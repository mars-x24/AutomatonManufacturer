using AtomicTorch.CBND.CoreMod.StaticObjects;
using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Manufacturers;
using AtomicTorch.CBND.CoreMod.Systems.Crafting;
using AtomicTorch.CBND.CoreMod.Systems.InteractionChecker;
using AtomicTorch.CBND.CoreMod.Systems.Notifications;
using AtomicTorch.CBND.GameApi.Data.World;
using AtomicTorch.CBND.GameApi.Scripting;
using AutomatonItemDetector.Scripts.AutomatonManufacturer.Features;
using System.Collections.Generic;
using System.Threading.Tasks;
using static AtomicTorch.CBND.CoreMod.Systems.Crafting.Recipe;

namespace CryoFall.AutomatonManufacturer.Recipes
{
  public class PasteRecipe
  {
    public static bool Paste(IWorldObject targetObject, List<Recipe> recipes)
    {
      if (CopyRecipe.Recipe is null)
        return false;

      if(recipes.Count == 1)
      {
        if (recipes[0] == CopyRecipe.Recipe)
          return false;
      }

      var obj = targetObject.ProtoGameObject as ProtoObjectManufacturer;
      if (obj is null)
        return false;

      if (CopyRecipe.Recipe is RecipeForManufacturing recipeManuf)
      {
        if (recipeManuf.StationTypes.Contains(targetObject.ProtoWorldObject))
        {
          obj.ClientSelectRecipe((IStaticWorldObject)targetObject, CopyRecipe.Recipe);

          recipes[0] = CopyRecipe.Recipe;

          SendNotification(targetObject);

          return true;
        }
      }

      return false;
    }

    public static void SendNotification(IWorldObject targetObject)
    {
      string title = "PASTE RECIPE (" + CopyRecipe.Recipe.Name + ")";
      string message = "Succeed - " + targetObject.ProtoWorldObject.Name;
      NotificationSystem.ClientShowNotification(title, message, NotificationColor.Neutral, null, null, true, true, false);
    }

  }
}
