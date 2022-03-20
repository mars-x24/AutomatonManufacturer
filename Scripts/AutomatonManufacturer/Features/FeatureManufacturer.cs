namespace CryoFall.AutomatonManufacturer.Features
{
  using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Crates;
  using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Manufacturers;
  using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Misc;
  using AtomicTorch.CBND.GameApi.Data;
  using AtomicTorch.CBND.GameApi.Scripting;
  using CryoFall.Automaton.ClientSettings;
  using CryoFall.Automaton.ClientSettings.Options;
  using System.Collections.Generic;
  using System.Linq;

  public class FeatureManufacturer : ProtoFeatureManufacturer<FeatureManufacturer>
  {
    private FeatureManufacturer() { }

    public override string Name => "AutoManufacturer";

    public override string Description => "Match up and take all items in manufacturing structures around you.";

    public List<IProtoEntity> ListManufacturerMatchUp { get; set; }
    public List<IProtoEntity> ListManufacturerTakeAll { get; set; }

    protected override void PrepareFeature(List<IProtoEntity> entityList, List<IProtoEntity> requiredItemList)
    {
      entityList = new List<IProtoEntity>(Api.FindProtoEntities<IProtoObjectManufacturer>());
      entityList.AddRange(Api.FindProtoEntities<IProtoObjectSprinkler>()); 

      ListManufacturerTakeAll = new List<IProtoEntity>(entityList);

      entityList.AddRange(Api.FindProtoEntities<IProtoObjectCrate>());

      ListManufacturerMatchUp = new List<IProtoEntity>(entityList);

      //requiredItemList.AddRange(Api.FindProtoEntities<IProtoItemToolToolbox>());
    }

    public override void PrepareOptions(SettingsFeature settingsFeature)
    {
      AddOptionIsEnabled(settingsFeature);
      Options.Add(new OptionSeparator());
      Options.Add(new OptionCheckBox(
               parentSettings: settingsFeature,
               id: "LowerFuelAmountFirst",
               label: "Match up less efficient fuel first",
               defaultValue: false,
               valueChangedCallback: value => { LowerFuelAmountFirst = value; }));
      Options.Add(new OptionSeparator());
      Options.Add(new OptionInformationText("Match Up"));
      Options.Add(new OptionEntityList(
          parentSettings: settingsFeature,
          id: "EnabledListManufacturerMatchUp",
          entityList: ListManufacturerMatchUp.OrderBy(entity => entity.Name),
          defaultEnabledList: new List<string>(),
          onEnabledListChanged: enabledList => EnabledListManufacturerMatchUp = enabledList));
      Options.Add(new OptionSeparator());
      Options.Add(new OptionInformationText("Take All"));
      Options.Add(new OptionEntityList(
          parentSettings: settingsFeature,
          id: "EnabledListManufacturerTakeAll",
          entityList: ListManufacturerTakeAll.OrderBy(entity => entity.Name),
          defaultEnabledList: new List<string>(),
          onEnabledListChanged: enabledList => EnabledListManufacturerTakeAll = enabledList));
    }
  }
}
