using AtomicTorch.CBND.CoreMod.Bootstrappers;
using AtomicTorch.CBND.CoreMod.ClientComponents.Input;
using AtomicTorch.CBND.GameApi.Data.Characters;
using AtomicTorch.CBND.GameApi.Scripting;
using AutomatonItemDetector.Scripts.AutomatonManufacturer.Features;

namespace CryoFall.AutomatonManufacturer
{
  public class AutomatonManufacturerBootstrapperClient : BaseBootstrapper
  {
    private static ClientInputContext gameplayInputContext;

    public override void ClientInitialize()
    {
      ClientInputManager.RegisterButtonsEnum<AutomatonManufacturerButton>();

      BootstrapperClientGame.InitEndCallback += GameInitHandler;

      BootstrapperClientGame.ResetCallback += ResetHandler;
    }

    private static void GameInitHandler(ICharacter currentCharacter)
    {
      gameplayInputContext = ClientInputContext
                             .Start("AutomatonManufacturer")
                             .HandleButtonDown(AutomatonManufacturerButton.CopyRecipe, CopyRecipe.Copy)
                             .HandleButtonDown(AutomatonManufacturerButton.CancelCopy, CopyRecipe.CancelWithNotification);
    }

    private static void ResetHandler()
    {
      gameplayInputContext?.Stop();
      gameplayInputContext = null;
    }


  }
}