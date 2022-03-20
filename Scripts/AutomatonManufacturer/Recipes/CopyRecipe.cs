using AtomicTorch.CBND.CoreMod.ClientComponents.Input;
using AtomicTorch.CBND.CoreMod.StaticObjects;
using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Manufacturers;
using AtomicTorch.CBND.CoreMod.Systems.Crafting;
using AtomicTorch.CBND.CoreMod.Systems.InteractionChecker;
using AtomicTorch.CBND.CoreMod.Systems.Notifications;
using AtomicTorch.CBND.CoreMod.UI.Controls.Core;
using AtomicTorch.CBND.CoreMod.UI.Controls.Game.HUD.Notifications;
using AtomicTorch.CBND.CoreMod.UI.Controls.Game.WorldObjects.Data;
using AtomicTorch.CBND.CoreMod.UI.Controls.Game.WorldObjects.Manufacturers.Data;
using AtomicTorch.CBND.CoreMod.UI.Services;
using AtomicTorch.CBND.GameApi.Data.World;
using AtomicTorch.CBND.GameApi.Scripting;
using CryoFall.AutomatonManufacturer;
using System;
using System.Collections.Generic;

namespace AutomatonItemDetector.Scripts.AutomatonManufacturer.Features
{
  class CopyRecipe
  {
    public static Recipe Recipe = null;
    private static HudNotificationControl NotificationControl = null;

    public static void Copy()
    {
      Cancel();

      CopyRecipeWithWindow();
      if (Recipe is null)
        CopyRecipeWithMousePosition();
    }

    private static void CopyRecipeWithWindow()
    {
      IStaticWorldObject targetObject = null;

      if (WindowsManager.OpenedWindowsCount == 0)
        return;

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

        if (targetObject is not null && targetObject.ClientHasPrivateState)
          break;
      }

      if (targetObject is not null)
      {
        ObjectManufacturerPrivateState privateState = targetObject.GetPrivateState<ObjectManufacturerPrivateState>();
        Recipe = privateState.ManufacturingState.SelectedRecipe;

        SendRecipeNotification();
      }
    }

    private static async void CopyRecipeWithMousePosition()
    {
      var CurrentCharacter = Api.Client.Characters.CurrentPlayerCharacter;

      Tile tile = Api.Client.World.TileAtCurrentMousePosition;
      if (tile.IsValidTile)
      {
        var tempList = new List<IStaticWorldObject>(tile.StaticObjects);
        foreach (var targetObject in tempList)
        {
          if (targetObject is null)
            continue;

          if (!targetObject.ProtoWorldObject.SharedCanInteract(CurrentCharacter, targetObject, false))
            continue;

          ObjectManufacturerPrivateState privateState = null;

          try
          {
            await InteractableWorldObjectHelper.ClientStartInteract(targetObject, false);
            if (targetObject.ClientHasPrivateState)
              privateState = targetObject.GetPrivateState<ObjectManufacturerPrivateState>();

            if (privateState is null)
              continue;

            if (privateState.ManufacturingState.SelectedRecipe is not null)
            {
              Recipe = privateState.ManufacturingState.SelectedRecipe;
              break;
            }
          }
          finally
          {
            InteractionCheckerSystem.SharedUnregister(CurrentCharacter, targetObject, isAbort: false);
          }
        }
      }

      if (Recipe is null)
      {
        string title = "COPY RECIPE";
        string message = "No recipe found at mouse position";
        NotificationSystem.ClientShowNotification(title, message, NotificationColor.Neutral, null, null, true, true, false);
      }
      else
      {
        SendRecipeNotification();
      }
    }

    private static void SendRecipeNotification()
    {
      if (Recipe is null)
        return;

      Action actionCancel = CancelWithNotification;

      string key = ClientInputManager.GetKeyForButton(AutomatonManufacturerButton.CancelCopy).ToString();
      string keyPaste = ClientInputManager.GetKeyForButton(AutomatonManufacturerButton.KeyHeld).ToString();
      string title = "COPY RECIPE (" + Recipe.Name + ")";
      string message = "Click here or press (" + key + ") to cancel.";
      message += "[br]";
      message += "Press (" + keyPaste + ") to paste recipe.";
      if (!CryoFall.Automaton.AutomatonManager.IsEnabled)
      {
        message += "[br]";
        message += "You must enable Automaton";
      }
      NotificationControl = NotificationSystem.ClientShowNotification(title, message, NotificationColor.Neutral, null, actionCancel, false, true, false);
    }

    public static void CancelWithNotification()
    {
      if (Recipe is not null)
      {
        string title = "COPY RECIPE (" + Recipe.Name + ")";
        string message = "CANCELED";
        NotificationSystem.ClientShowNotification(title, message, NotificationColor.Neutral, null, null, true, true, false);
      }

      Cancel();
    }

    public static void Cancel()
    {
      if (NotificationControl is not null)
        NotificationControl.Hide(true);

      Recipe = null;
    }

  }
}
