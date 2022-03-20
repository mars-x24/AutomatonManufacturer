using AtomicTorch.CBND.CoreMod.Characters.Player;
using AtomicTorch.CBND.CoreMod.SoundPresets;
using AtomicTorch.CBND.CoreMod.Systems.Crafting;
using AtomicTorch.CBND.GameApi.Data.Characters;
using AtomicTorch.CBND.GameApi.Data.Items;
using AtomicTorch.CBND.GameApi.Data.World;
using AtomicTorch.CBND.GameApi.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CryoFall.AutomatonManufacturer.Recipes
{
  public class RecipeManager
  {
    private List<Recipe> recipes;
    private List<IItemsContainer> receivingContainers;
    private IWorldObject targetObject;
    private ICharacter currentCharacter;

    public RecipeManager(IWorldObject targetObject, List<Recipe> recipes, List<IItemsContainer> receivingContainers)
    {
      this.targetObject = targetObject;
      this.recipes = recipes;
      this.receivingContainers = receivingContainers;
      this.currentCharacter = Api.Client.Characters.CurrentPlayerCharacter;
    }

    public bool MatchUpItemsWithRecipe()
    {
      bool hasRecipe = false;

      for (int i = 0; i < this.receivingContainers.Count; i++)
      {
        if (this.MatchUpItemsWithRecipe(this.receivingContainers[i], this.recipes[i]))
          hasRecipe = true;
      }

      return hasRecipe;
    }

    private bool MatchUpItemsWithRecipe(IItemsContainer itemContainer, Recipe recipe)
    {
      if (recipe is null)
        return false;

      var playerPrivateState = PlayerCharacter.GetPrivateState(this.currentCharacter);
      var playerInventory = playerPrivateState.ContainerInventory;
      var playerHotbar = playerPrivateState.ContainerHotbar;

      var sourcesContainer = new List<IItemsContainer>();
      sourcesContainer.Add(playerInventory);
      sourcesContainer.Add(playerHotbar);

      var isAtLeastOneItemMoved = false;

      int recipeCount = this.GetRecipeCount(itemContainer, recipe);

      Dictionary<IProtoItem, ushort> recipeItemMoveCount = new Dictionary<IProtoItem, ushort>();

      //get item count
      foreach (var recipeItem in recipe.InputItems)
      {
        ushort currentCount = Convert.ToUInt16(itemContainer.CountItemsOfType(recipeItem.ProtoItem));
        int moveCount = recipeItem.Count * recipeCount;

        moveCount -= currentCount;

        if (moveCount == 0)
          continue;

        if (moveCount < 0)
        {
          //too much of an item, remove some
          ushort moveCountDiff = Convert.ToUInt16(Math.Abs(moveCount));

          IEnumerable<IItem> itemsToMove = itemContainer.GetItemsOfProto(recipeItem.ProtoItem);
          foreach (var itemToMove in itemsToMove)
          {
            ushort itemCount = itemToMove.Count;
            ushort itemMoveCount = Math.Min(itemCount, moveCountDiff);

            if (itemMoveCount == 0)
              break;

            if (this.targetObject.ProtoWorldObject.SharedCanInteract(this.currentCharacter, this.targetObject, false))
            {
              if (sourcesContainer.Any(it => Api.Client.Items.MoveOrSwapItem(itemToMove, it, countToMove: itemMoveCount, isLogErrors: false)))
              {
                isAtLeastOneItemMoved = true;
                moveCountDiff -= itemMoveCount;
              }
            }
          }

          continue;
        }

        recipeItemMoveCount[recipeItem.ProtoItem] = Convert.ToUInt16(moveCount);
      }

      //match up items
      foreach (var protoItem in recipeItemMoveCount.Keys)
      {
        ushort moveCount = recipeItemMoveCount[protoItem];

        List<IItem> itemsToMove = new List<IItem>();
        foreach (IItemsContainer it in sourcesContainer)
          itemsToMove.AddRange(it.GetItemsOfProto(protoItem));

        if (itemsToMove.Count == 0)
          continue;

        ushort count = Convert.ToUInt16(itemsToMove.Sum(it => it.Count));

        moveCount = Math.Min(count, moveCount);
        if (moveCount <= 0)
          continue;

        foreach (IItem itemToMove in itemsToMove)
        {
          ushort itemCount = itemToMove.Count;
          ushort itemMoveCount = Math.Min(itemCount, moveCount);

          if (itemMoveCount == 0)
            break;

          //get useless item slot
          byte? uselessItemSlot = null;
          bool allowSwapping = false;
          if (itemMoveCount == itemToMove.Count)
          {
            uselessItemSlot = this.GetRecipeUselessItemSlot(itemContainer, recipe);
            allowSwapping = uselessItemSlot.HasValue;
          }

          if (this.targetObject.ProtoWorldObject.SharedCanInteract(this.currentCharacter, this.targetObject, false))
          {
            if (Api.Client.Items.MoveOrSwapItem(itemToMove, itemContainer, slotId: uselessItemSlot, countToMove: itemMoveCount, allowSwapping: allowSwapping, isLogErrors: false))
            {
              isAtLeastOneItemMoved = true;
              moveCount -= itemMoveCount;
            }
          }
        }
      }

      //try to remove useless items
      if (this.RecipeRemoveUselessItems(itemContainer, recipe, sourcesContainer))
        isAtLeastOneItemMoved = true;

      if (isAtLeastOneItemMoved) ItemsSoundPresets.ItemGeneric.PlaySound(ItemSound.Drop);

      return true;
    }

    private byte? GetRecipeUselessItemSlot(IItemsContainer itemContainer, Recipe recipe)
    {
      foreach (var item in itemContainer.Items)
      {
        if (!recipe.InputItems.Any(it => it.ProtoItem == item.ProtoItem))
          return item.ContainerSlotId;
      }

      return null;
    }

    private bool RecipeRemoveUselessItems(IItemsContainer itemContainer, Recipe recipe, List<IItemsContainer> sourcesContainer)
    {
      bool isAtLeastOneItemMoved = false;

      foreach (var item in itemContainer.Items)
      {
        if (!recipe.InputItems.Any(it => it.ProtoItem == item.ProtoItem))
        {
          if (this.targetObject.ProtoWorldObject.SharedCanInteract(this.currentCharacter, this.targetObject, false))
          {
            if (sourcesContainer.Any(it => Api.Client.Items.MoveOrSwapItem(item, it, isLogErrors: false)))
              isAtLeastOneItemMoved = true;
          }
        }
      }

      return isAtLeastOneItemMoved;
    }

    private int GetRecipeCount(IItemsContainer itemContainer, Recipe recipe)
    {
      int slotCount = itemContainer.SlotsCount;
      int slotFactor = (int)Math.Floor((double)slotCount / (double)recipe.InputItems.Length);
      int itemFactor = int.MaxValue;
      foreach (var recipeItem in recipe.InputItems)
        itemFactor = Math.Min(itemFactor, recipeItem.ProtoItem.MaxItemsPerStack / recipeItem.Count);
      if (itemFactor == int.MaxValue)
        itemFactor = 1;

      int factor = slotFactor * itemFactor;
      int slotsNeeded = this.GetRecipeSlotCount(factor, recipe);
      if (slotsNeeded == slotCount)
        return factor;

      int testFactor = (int)Math.Floor((double)slotCount / (double)slotsNeeded * (double)factor);
      slotsNeeded = this.GetRecipeSlotCount(testFactor, recipe);
      if (slotsNeeded <= slotCount)
        factor = testFactor;
      else
      {
        do
        {
          testFactor--;
          slotsNeeded = this.GetRecipeSlotCount(testFactor, recipe);
        }
        while (slotsNeeded > slotCount);

        factor = testFactor;
      }

      return factor;
    }

    private int GetRecipeSlotCount(int factor, Recipe recipe)
    {
      int count = 0;
      foreach (var recipeItem in recipe.InputItems)
        count += (int)Math.Ceiling((double)(recipeItem.Count * factor) / (double)recipeItem.ProtoItem.MaxItemsPerStack);
      return count;
    }

  }
}
