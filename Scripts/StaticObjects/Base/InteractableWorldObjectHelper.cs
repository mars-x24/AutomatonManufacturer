namespace AtomicTorch.CBND.CoreMod.StaticObjects
{
  using AtomicTorch.CBND.CoreMod.Systems.InteractionChecker;
  using AtomicTorch.CBND.CoreMod.UI.Controls.Core;
  using AtomicTorch.CBND.CoreMod.UI.Controls.Core.Menu;
  using AtomicTorch.CBND.GameApi.Data;
  using AtomicTorch.CBND.GameApi.Data.Characters;
  using AtomicTorch.CBND.GameApi.Data.World;
  using AtomicTorch.CBND.GameApi.Scripting;
  using AtomicTorch.CBND.GameApi.Scripting.Network;
  using System;
  using System.Collections;
  using System.Threading.Tasks;

  public class InteractableWorldObjectHelper : ProtoEntity
  {
    private static InteractableWorldObjectHelper instance;

    private static int lastRequestId;

    private Hashtable isAwaitingServerInteraction = new Hashtable();

    public delegate void DelegateClientMenuCreated(
        IWorldObject worldObject,
        BaseUserControlWithWindow menu);

    public static event DelegateClientMenuCreated ClientMenuCreated;

    public override string Name => nameof(InteractableWorldObjectHelper);

    public static Task ClientStartInteract(IWorldObject worldObject)
    {
      return instance.ClientInteractStartAsync(worldObject, true);
    }

    public static Task ClientStartInteract(IWorldObject worldObject, bool openUI)
    {
      return instance.ClientInteractStartAsync(worldObject, openUI);
    }

    public static void ServerTryAbortInteraction(ICharacter character, IWorldObject worldObject)
    {
      InteractionCheckerSystem.SharedUnregister(character, worldObject, isAbort: true);
    }

    protected override void PrepareProto()
    {
      base.PrepareProto();
      instance = this;
    }

    private static IInteractableProtoWorldObject SharedGetProto(IWorldObject worldObject)
    {
      return (IInteractableProtoWorldObject)worldObject.ProtoGameObject;
    }

    private async Task ClientInteractStartAsync(IWorldObject worldObject)
    {
      this.ClientInteractStartAsync(worldObject, true);
    }

    private async Task ClientInteractStartAsync(IWorldObject worldObject, bool openUI)
    {
      if (this.isAwaitingServerInteraction.ContainsKey(worldObject) && (bool)this.isAwaitingServerInteraction[worldObject])
      {
        return;
      }

      var character = Client.Characters.CurrentPlayerCharacter;
      if (InteractionCheckerSystem.SharedGetCurrentInteraction(character) == worldObject)
      {
        // already interacting with this object
        return;
      }

      this.isAwaitingServerInteraction[worldObject] = true;
      try
      {
        var requestId = ++lastRequestId;
        var isOpened = await this.CallServer(_ => _.ServerRemote_OnClientInteractStart(worldObject));
        if (!isOpened
            || requestId != lastRequestId)
        {
          return;
        }
      }
      finally
      {
        this.isAwaitingServerInteraction.Remove(worldObject);
      }

      if (openUI)
      {
        var objectWindow = SharedGetProto(worldObject).ClientOpenUI(worldObject);
        if (objectWindow is null)
        {
          Logger.Info("Cannot open menu for object interaction with " + worldObject);
          this.CallServer(_ => _.ServerRemote_OnClientInteractFinish(worldObject));
          return;
        }

        Api.SafeInvoke(() => ClientMenuCreated?.Invoke(worldObject, objectWindow));
        if (!(objectWindow is IMenu))
        {
          ClientCurrentInteractionMenu.RegisterMenuWindow(objectWindow);
        }
        else
        {
          ClientCurrentInteractionMenu.TryCloseCurrentMenu();
        }

        InteractionCheckerSystem.SharedRegister(
            character,
            worldObject,
            finishAction: _ => objectWindow.CloseWindow());

        ClientInteractionUISystem.Register(
            worldObject,
            objectWindow,
            onMenuClosedByClient:
            () =>
            {
              InteractionCheckerSystem.SharedUnregister(character, worldObject, isAbort: false);
              if (!worldObject.IsDestroyed)
              {
                ++lastRequestId;
                this.CallServer(_ => _.ServerRemote_OnClientInteractFinish(worldObject));
              }
            });

        Logger.Info("Started object interaction with " + worldObject);
        if (objectWindow is IMenu objectMenu)
        {
          if (!objectMenu.IsOpened)
          {
            objectMenu.Toggle();
          }
        }
        else
        {
          ClientCurrentInteractionMenu.Open();
        }
      }
      else
      {
        InteractionCheckerSystem.SharedRegister(character, worldObject, null);
      }
    }

    public static void ClientFinishInteract(IWorldObject worldObject)
    {
      instance.ClientInteractFinish(worldObject);
    }

    private void ClientInteractFinish(IWorldObject worldObject)
    {
      this.CallServer(_ => _.ServerRemote_OnClientInteractFinish(worldObject));
    }

    private void ClientRemote_FinishInteraction(IWorldObject worldObject)
    {
      ClientInteractionUISystem.OnServerForceFinishInteraction(worldObject);
    }

    private void ServerFinishInteractionInternal(ICharacter who, IWorldObject worldObject)
    {
      if (worldObject.IsDestroyed)
      {
        return;
      }

      Server.World.ExitPrivateScope(who, worldObject);

      try
      {
        SharedGetProto(worldObject).ServerOnMenuClosed(who, worldObject);
      }
      catch (Exception ex)
      {
        Logger.Exception(
            ex,
            "Exception when calling " + nameof(IInteractableProtoWorldObject.ServerOnMenuClosed));
      }

      Logger.Info($"Finished object interaction with {worldObject} for {who}");
    }

    private void ServerRemote_OnClientInteractFinish(IWorldObject worldObject)
    {
      var character = ServerRemoteContext.Character;
      if (!InteractionCheckerSystem.SharedUnregister(character, worldObject, isAbort: false))
      {
        return;
      }

      Logger.Info($"Client {character} informed that the object interaction with {worldObject} is finished");
      this.ServerFinishInteractionInternal(character, worldObject);
    }

    private bool ServerRemote_OnClientInteractStart(IWorldObject worldObject)
    {
      var character = ServerRemoteContext.Character;

      if (worldObject is null
          || !worldObject.ProtoWorldObject.SharedCanInteract(character, worldObject, writeToLog: true))
      {
        // player is too far from the world object or world object is destroyed
        return false;
      }

      InteractionCheckerSystem.SharedAbortCurrentInteraction(character);

      var proto = SharedGetProto(worldObject);

      proto.ServerOnClientInteract(character, worldObject);

      if (proto.IsAutoEnterPrivateScopeOnInteraction)
      {
        // enter private scope - containers will be sent to the player character
        Server.World.EnterPrivateScope(character, worldObject);
      }

      // register private scope exit on interaction cancel
      InteractionCheckerSystem.SharedRegister(
          character,
          worldObject,
          finishAction: isAbort =>
                        {
                          if (worldObject.IsDestroyed)
                          {
                            return;
                          }

                          this.ServerFinishInteractionInternal(character, worldObject);

                          if (isAbort)
                          {
                            // notify client
                            this.CallClient(character, _ => this.ClientRemote_FinishInteraction(worldObject));
                          }
                        });

      Logger.Info($"Started object interaction with {worldObject} for {character}");
      return true;
    }
  }
}