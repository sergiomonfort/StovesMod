using StovesMod.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace StovesMod
{
    public class StovesModModSystem : ModSystem
    {
        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            Mod.Logger.Notification("Hello from template mod: " + api.Side);
            api.RegisterBlockEntityClass(Mod.Info.ModID + ".trampoline", typeof(BlockEntityTrampoline));
            api.RegisterBlockEntityClass(Mod.Info.ModID + ".6stove", typeof(BlockEntity6stove));
            //api.RegisterBlockClass(Mod.Info.ModID + ".trampoline", typeof(BlockEntityTrampoline));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            Mod.Logger.Notification("Hello from template mod server side: " + Lang.Get("stovesMod:hello"));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            Mod.Logger.Notification("Hello from template mod client side: " + Lang.Get("stovesMod:hello"));
        }
    }
}
