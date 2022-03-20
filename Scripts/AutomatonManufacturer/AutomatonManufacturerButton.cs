using AtomicTorch.CBND.CoreMod.ClientComponents.Input;
using AtomicTorch.CBND.GameApi;
using AtomicTorch.CBND.GameApi.ServicesClient;
using System.ComponentModel;

namespace CryoFall.AutomatonManufacturer
{
  [NotPersistent]
  public enum AutomatonManufacturerButton
  {
    [Description("Activate Key Held")]
    [ButtonInfo(InputKey.Space, Category = "Manufacturer")]
    KeyHeld,

    [Description("Copy recipe")]
    [ButtonInfo(InputKey.Add, Category = "Manufacturer")]
    CopyRecipe,

    [Description("Cancel copy")]
    [ButtonInfo(InputKey.Subtract, Category = "Manufacturer")]
    CancelCopy
  }
}