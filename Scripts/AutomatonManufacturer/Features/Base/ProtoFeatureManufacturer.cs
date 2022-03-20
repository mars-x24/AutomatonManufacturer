namespace CryoFall.AutomatonManufacturer.Features
{
  using AtomicTorch.CBND.CoreMod.Characters.Player;
  using AtomicTorch.CBND.CoreMod.ClientComponents.Input;
  using AtomicTorch.CBND.CoreMod.CraftRecipes;
  using AtomicTorch.CBND.CoreMod.CraftRecipes.Sprinkler;
  using AtomicTorch.CBND.CoreMod.Items.Generic;
  using AtomicTorch.CBND.CoreMod.SoundPresets;
  using AtomicTorch.CBND.CoreMod.StaticObjects;
  using AtomicTorch.CBND.CoreMod.StaticObjects.Structures;
  using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Barrels;
  using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Crates;
  using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Generators;
  using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Manufacturers;
  using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Misc;
  using AtomicTorch.CBND.CoreMod.Systems.Crafting;
  using AtomicTorch.CBND.CoreMod.Systems.InteractionChecker;
  using AtomicTorch.CBND.CoreMod.UI.Controls.Core;
  using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Items.Managers;
  using AtomicTorch.CBND.CoreMod.UI.Controls.Game.WorldObjects.Data;
  using AtomicTorch.CBND.CoreMod.UI.Controls.Game.WorldObjects.Manufacturers.Data;
  using AtomicTorch.CBND.CoreMod.UI.Services;
  using AtomicTorch.CBND.GameApi.Data;
  using AtomicTorch.CBND.GameApi.Data.Items;
  using AtomicTorch.CBND.GameApi.Data.World;
  using AtomicTorch.CBND.GameApi.Scripting;
  using AtomicTorch.CBND.GameApi.ServicesClient;
  using AtomicTorch.GameEngine.Common.Primitives;
  using CryoFall.Automaton.Features;
  using CryoFall.AutomatonManufacturer.Recipes;
  using System;
  using System.Collections.Generic;
  using System.Linq;

  public abstract class ProtoFeatureManufacturer<T> : ProtoFeature<T>
    where T : class
  {
    public List<IProtoEntity> EnabledListManufacturerMatchUp { get; set; }
    public List<IProtoEntity> EnabledListManufacturerTakeAll { get; set; }

    public bool LowerFuelAmountFirst = false;

    private List<IItemsContainer> inputContainers = new List<IItemsContainer>();
    private List<IItemsContainer> outputContainers = new List<IItemsContainer>();
    private List<IItemsContainer> fuelContainers = new List<IItemsContainer>();
    private List<Recipe> recipes = new List<Recipe>();

    private IProtoEntity iconItem = null;
    private LiquidType? liquidType = null;
    private bool liquidMaxed = false;

    private bool isInsertingWithoutMatch = false;

    /// <summary>
    /// Called by client component every tick.
    /// </summary>
    public override void Update(double deltaTime)
    {

    }

    public override void Stop()
    {
      base.Stop();
    }

    /// <summary>
    /// Called by client component on specific time interval.
    /// </summary>
    public override void Execute()
    {
      if (!(IsEnabled && CheckPrecondition()))
        return;

      this.FindTarget();
    }

    private void FindTarget()
    {
      if (!FindTargetByGameWindow())
        FindTargetByPosition();
    }

    protected override bool CheckPrecondition()
    {
      if (!base.CheckPrecondition())
        return false;

      InputKey inputKey = ClientInputManager.GetKeyForButton(AutomatonManufacturerButton.KeyHeld);
      if (!Api.Client.Input.IsKeyHeld(inputKey, true))
        return false;

      return true;
    }

    private void FindTargetByPosition()
    {
      var fromPos = CurrentCharacter.Position;
      using var objectsNearby = this.CurrentCharacter.PhysicsBody.PhysicsSpace
                                    .TestRectangle(new Vector2D(fromPos.X - 3.0, fromPos.Y - 3.0), new Vector2D(6.0, 6.0), null, false);

      List<IProtoEntity> enabledList = new List<IProtoEntity>();
      enabledList.AddRange(this.EnabledListManufacturerMatchUp);
      enabledList.AddRange(this.EnabledListManufacturerTakeAll);

      var objectOfInterest = objectsNearby.AsList()
                             ?.Where(t => enabledList.Contains(t.PhysicsBody?.AssociatedWorldObject?.ProtoGameObject))
                             .OrderBy(t => Math.Abs(((t.PhysicsBody.Position + t.PhysicsBody.CenterOffset) - CurrentCharacter.Position).Length))
                             .ToList();

      if (objectOfInterest == null || objectOfInterest.Count == 0)
        return;

      foreach (var obj in objectOfInterest)
      {
        var targetObject = obj.PhysicsBody.AssociatedWorldObject as IStaticWorldObject;

        if (!targetObject.ProtoWorldObject.SharedCanInteract(CurrentCharacter, targetObject, false))
          continue;

        this.ExecuteManufacturer(targetObject);
      }
    }

    private async void ExecuteManufacturer(IWorldObject targetObject)
    {
      if (object.Equals(targetObject, null))
        return;

      StructurePrivateState privateState = null;

      await InteractableWorldObjectHelper.ClientStartInteract(targetObject, false);
      if (targetObject.ClientHasPrivateState)
      {
        privateState = targetObject.GetPrivateState<StructurePrivateState>();

        this.AddContaintersRecipe(privateState);

        PasteRecipe.Paste(targetObject, this.recipes);

        StaticObjectPublicState publicState = targetObject.GetPublicState<StaticObjectPublicState>();

        this.ExecuteManufacturer(targetObject, publicState, privateState, WindowsManager.OpenedWindowsCount == 0);
      }
    }

    private bool FindTargetByGameWindow()
    {
      IStaticWorldObject targetObject = null;

      if (WindowsManager.OpenedWindowsCount > 0)
      {
        foreach (GameWindow gameWindow in WindowsManager.OpenedWindows)
        {
          if (gameWindow.DataContext is ViewModelWindowManufacturer viewModel)
            targetObject = viewModel.WorldObjectManufacturer;
          else if (gameWindow.DataContext is ViewModelWindowOilRefinery viewModelOilRefinery)
            targetObject = viewModelOilRefinery.WorldObjectManufacturer;
          else if (gameWindow.DataContext is ViewModelWindowOilCrackingPlant viewModelOilCrackingPlant)
            targetObject = viewModelOilCrackingPlant.WorldObjectManufacturer;
          else if (gameWindow.DataContext is ViewModelWindowCrateContainer viewModelCrate)
            targetObject = viewModelCrate.WorldObjectCrate;
          else if (gameWindow.DataContext is ViewModelWindowFridge viewModelFridge)
            targetObject = viewModelFridge.WorldObjectFridge;
          else if (gameWindow.DataContext is ViewModelWindowSprinkler viewModelSprinker)
            targetObject = viewModelSprinker.worldObject;

          if (targetObject is not null && targetObject.ClientHasPrivateState)
            break;
        }
      }

      if (targetObject is not null)
      {
        try
        {
          StructurePrivateState privateState = targetObject.GetPrivateState<StructurePrivateState>();
          StaticObjectPublicState publicState = targetObject.GetPublicState<StaticObjectPublicState>();

          this.AddContaintersRecipe(privateState);

          PasteRecipe.Paste(targetObject, this.recipes);

          this.ExecuteManufacturer(targetObject, publicState, privateState, false);

          return true;
        }
        catch { }
      }

      return false;
    }

    private bool ExecuteManufacturer(IWorldObject targetObject, StaticObjectPublicState publicState, StructurePrivateState privateState, bool unregister)
    {
      if (object.Equals(targetObject, null))
        return true;

      if (privateState is null)
        return true;

      this.Reset();

      this.AddContainersProtoObject(targetObject, publicState, privateState);

      this.SetInsertWithoutMatch(targetObject);

      this.ExecuteCommandTakeAllAndMatchUp(targetObject);

      if (unregister)
        InteractionCheckerSystem.SharedUnregister(CurrentCharacter, targetObject, isAbort: false);

      return true;
    }

    private void Reset()
    {
      this.inputContainers.Clear();
      this.outputContainers.Clear();
      this.fuelContainers.Clear();

      this.iconItem = null;
      this.liquidType = null;
      this.liquidMaxed = false;
    }

    private void AddContainersProtoObject(IWorldObject targetObject, StaticObjectPublicState publicState, StructurePrivateState privateState)
    {
      if (privateState is ObjectManufacturerPrivateState prManufacturer)
      {
        this.inputContainers.Add(prManufacturer.ManufacturingState.ContainerInput);
        this.outputContainers.Add(prManufacturer.ManufacturingState.ContainerOutput);
        if (prManufacturer.FuelBurningState is not null)
          this.fuelContainers.Add(prManufacturer.FuelBurningState.ContainerFuel);
      }
      else if (privateState is ObjectCratePrivateState prCrate)
      {
        this.inputContainers.Add(prCrate.ItemsContainer);
      }

      if (privateState is ProtoObjectOilRefinery.PrivateState prOilRefinery)
      {
        this.inputContainers.Add(prOilRefinery.ManufacturingStateGasoline.ContainerInput);
        this.inputContainers.Add(prOilRefinery.ManufacturingStateMineralOil.ContainerInput);

        this.outputContainers.Add(prOilRefinery.ManufacturingStateGasoline.ContainerOutput);
        this.outputContainers.Add(prOilRefinery.ManufacturingStateMineralOil.ContainerOutput);
      }
      else if (privateState is ProtoObjectOilCrackingPlant.PrivateState prOilCrackingPlant)
      {
        this.inputContainers.Add(prOilCrackingPlant.ManufacturingStateGasoline.ContainerInput);

        this.outputContainers.Add(prOilCrackingPlant.ManufacturingStateGasoline.ContainerOutput);
      }
      else if (privateState is ProtoObjectSprinkler.PrivateState prSprinkler)
      {
        this.inputContainers.Add(prSprinkler.ManufacturingState.ContainerInput);

        this.outputContainers.Add(prSprinkler.ManufacturingState.ContainerOutput);
      }

      if (publicState is ObjectCratePublicState puCrate)
      {
        if (puCrate.IconSource is not null)
          this.iconItem = puCrate.IconSource;
      }

      if (targetObject.ProtoGameObject is ProtoObjectBarrel barrel && publicState is ProtoBarrelPublicState puBarrel && privateState is ProtoBarrelPrivateState prBarrel)
      {
        this.liquidMaxed = prBarrel.LiquidAmount == barrel.LiquidCapacity;
        this.liquidType = puBarrel.LiquidType;
      }
    }

    public void AddContaintersRecipe(StructurePrivateState privateState)
    {
      this.recipes.Clear();

      if (privateState is ObjectManufacturerPrivateState prManufacturer)
      {
        this.recipes.Add(prManufacturer.ManufacturingState.SelectedRecipe);
      }
      else if (privateState is ObjectCratePrivateState prCrate)
      {
        this.recipes.Add(null);
      }


      if (privateState is ProtoObjectOilRefinery.PrivateState prOilRefinery)
      {
        this.recipes.Add(prOilRefinery.ManufacturingStateGasoline.SelectedRecipe ?? Api.GetProtoEntity<RecipeOilRefineryGasolineCanister>());
        this.recipes.Add(prOilRefinery.ManufacturingStateMineralOil.SelectedRecipe ?? Api.GetProtoEntity<RecipeOilRefineryMineralOilCanister>());
      }
      else if (privateState is ProtoObjectOilCrackingPlant.PrivateState prOilCrackingPlant)
      {
        this.recipes.Add(prOilCrackingPlant.ManufacturingStateGasoline.SelectedRecipe);
      }
      else if (privateState is ProtoObjectSprinkler.PrivateState prSprinkler)
      {
        this.recipes.Add(prSprinkler.ManufacturingState.SelectedRecipe ?? Api.GetProtoEntity<RecipeSprinklerEmptyBottleFromWaterBottle>());
      }
    }

    private void SetInsertWithoutMatch(IWorldObject targetObject)
    {
      if (!(targetObject is IStaticWorldObject))
        return;

      this.isInsertingWithoutMatch = ((IStaticWorldObject)targetObject).ProtoStaticWorldObject switch
      {
        ProtoObjectWell _ => true,
        ProtoObjectOilPump _ => true,
        ObjectGeneratorEngine _ => true,
        ObjectOilCrackingPlant _ => true,
        ObjectOilRefinery _ => true,
        ObjectSprinkler _=> true,
        _ => false
      };
    }

    private void ExecuteCommandTakeAllAndMatchUp(IWorldObject targetObject)
    {
      ExecuteCommandTakeAll(targetObject);
      if (ExecuteCommandRecipe(targetObject))
        ExecuteCommandMatch(targetObject, false); //get back full liquid
      else
        ExecuteCommandMatch(targetObject, true);
    }

    private void ExecuteCommandTakeAll(IWorldObject targetObject)
    {
      if (!this.EnabledListManufacturerTakeAll.Contains(targetObject.ProtoGameObject))
        return;

      ExecuteCommandMatch(targetObject, false);
      foreach (var container in outputContainers)
      {
        if (!targetObject.ProtoWorldObject.SharedCanInteract(CurrentCharacter, targetObject, false))
          return;

        CurrentCharacter.ProtoCharacter.ClientTryTakeAllItems(CurrentCharacter, container, showNotificationIfInventoryFull: true);
      }
    }

    private bool ExecuteCommandRecipe(IWorldObject targetObject)
    {
      if (!this.EnabledListManufacturerMatchUp.Contains(targetObject.ProtoGameObject))
        return false;

      var receivingContainers = new List<IItemsContainer>();
      receivingContainers.AddRange(inputContainers);
      foreach (var receivingContainer in receivingContainers)
        ClientContainerSortHelper.ConsolidateItemStacks((IClientItemsContainer)receivingContainer);

      var receivingFuelContainers = new List<IItemsContainer>();
      receivingFuelContainers.AddRange(fuelContainers);
      foreach (var receivingFuelContainer in receivingFuelContainers)
        ClientContainerSortHelper.ConsolidateItemStacks((IClientItemsContainer)receivingFuelContainer);

      var playerPrivateState = PlayerCharacter.GetPrivateState(CurrentCharacter);
      var playerInventory = playerPrivateState.ContainerInventory;
      var playerHotbar = playerPrivateState.ContainerHotbar;

      List<IItem> sourceItems = new List<IItem>();
      sourceItems.AddRange(playerInventory.Items);
      sourceItems.AddRange(playerHotbar.Items);

      //move fuel without match
      this.MatchUpFuelSolidWithoutMatch(sourceItems, targetObject, receivingFuelContainers);

      //try move with recipe
      RecipeManager recipeManager = new RecipeManager(targetObject, this.recipes, receivingContainers);
      return recipeManager.MatchUpItemsWithRecipe();
    }


    private void ExecuteCommandMatch(IWorldObject targetObject, bool isUp)
    {
      if (!this.EnabledListManufacturerMatchUp.Contains(targetObject.ProtoGameObject))
        return;

      var playerPrivateState = PlayerCharacter.GetPrivateState(CurrentCharacter);
      var playerInventory = playerPrivateState.ContainerInventory;
      var playerHotbar = playerPrivateState.ContainerHotbar;

      var receivingContainers = new List<IItemsContainer>();
      var receivingRecipes = new List<Recipe>();

      List<IItem> sourceItems = new List<IItem>();

      if (isUp)
      {
        // move items "up" - from player inventory to this crate container
        sourceItems.AddRange(playerInventory.Items);
        //sourceItems.AddRange(playerHotbar.Items);

        receivingContainers.AddRange(inputContainers);
        foreach (var receivingContainer in receivingContainers)
          ClientContainerSortHelper.ConsolidateItemStacks((IClientItemsContainer)receivingContainer);

        //move without match
        if (this.isInsertingWithoutMatch)
          this.MatchUpItemsWithoutMatch(sourceItems, targetObject, receivingContainers);
        else
          //move with match
          this.MatchItems(sourceItems, targetObject, receivingContainers);
      }
      else
      {
        // move items "down" - from this crate container to player containers
        sourceItems.AddRange(outputContainers.SelectMany(i => i.Items));

        if (this.liquidType.HasValue && this.liquidMaxed)
        {
          foreach (var container in inputContainers)
            sourceItems.AddRange(container.Items);
        }
        receivingContainers.Add(playerInventory);
        receivingContainers.Add(playerHotbar);

        this.MatchItems(sourceItems, targetObject, receivingContainers);
      }
    }

    private bool MatchUpFuelSolidWithoutMatch(List<IItem> sourceItems, IWorldObject targetObject, List<IItemsContainer> receivingContainers)
    {
      if (receivingContainers.Count == 0)
        return false;

      var isAtLeastOneItemMoved = false;

      var temp = sourceItems
        .Where(i => i.ProtoItem is IProtoItemFuelSolid)
        .OrderBy(i => LowerFuelAmountFirst ? ((IProtoItemFuelSolid)i.ProtoItem).FuelAmount : -((IProtoItemFuelSolid)i.ProtoItem).FuelAmount).ToList();

      foreach (var itemToMove in temp)
      {
        if (!targetObject.ProtoWorldObject.SharedCanInteract(CurrentCharacter, targetObject, false))
          return false;

        if (receivingContainers.Any(it => Api.Client.Items.MoveOrSwapItem(itemToMove, it, allowSwapping: false, isLogErrors: false)))
          isAtLeastOneItemMoved = true;
      }

      if (isAtLeastOneItemMoved) ItemsSoundPresets.ItemGeneric.PlaySound(ItemSound.Drop);

      return true;
    }

    private bool MatchUpItemsWithoutMatch(List<IItem> sourceItems, IWorldObject targetObject, List<IItemsContainer> receivingContainers)
    {
      if (receivingContainers.Count == 0)
        return false;

      var isAtLeastOneItemMoved = false;

      foreach (var itemToMove in sourceItems)
      {
        if (!targetObject.ProtoWorldObject.SharedCanInteract(CurrentCharacter, targetObject, false))
          return false;

        if (receivingContainers.Any(it => Api.Client.Items.MoveOrSwapItem(itemToMove, it, allowSwapping: false, isLogErrors: false)))
          isAtLeastOneItemMoved = true;
      }

      if (isAtLeastOneItemMoved) ItemsSoundPresets.ItemGeneric.PlaySound(ItemSound.Drop);

      return true;
    }

    private void MatchItems(List<IItem> sourceItems, IWorldObject targetObject, List<IItemsContainer> receivingContainers)
    {
      var isAtLeastOneItemMoved = false;

      var itemTypesToMove = new HashSet<IProtoItem>(receivingContainers.SelectMany(i => i.Items).Select(i => i.ProtoItem));

      var itemsToMove = sourceItems
          .Where(item => (itemTypesToMove.Contains(item.ProtoItem) || this.MoveIconItem(item) || this.MoveItemWithLiquidType(item)))
          .OrderBy(i => i.ProtoItem.Id)
          .ToList();

      foreach (var itemToMove in itemsToMove)
      {
        if (!targetObject.ProtoWorldObject.SharedCanInteract(CurrentCharacter, targetObject, false))
          return;

        if (receivingContainers.Any(it => Api.Client.Items.MoveOrSwapItem(itemToMove, it, allowSwapping: false, isLogErrors: false)))
          isAtLeastOneItemMoved = true;
      }

      if (isAtLeastOneItemMoved) ItemsSoundPresets.ItemGeneric.PlaySound(ItemSound.Drop);
    }

    private bool MoveIconItem(IItem item)
    {
      return item.ProtoGameObject == this.iconItem;
    }

    private bool MoveItemWithLiquidType(IItem item)
    {
      if (this.liquidMaxed)
        return false;

      return this.ItemHasLiquidType(item);
    }

    private bool ItemHasLiquidType(IItem item)
    {
      if (!this.liquidType.HasValue)
        return false;

      if (!(item.ProtoGameObject is IProtoItemLiquidStorage itemWithLiquid))
        return false;

      return itemWithLiquid.LiquidType == this.liquidType;
    }

  }
}
