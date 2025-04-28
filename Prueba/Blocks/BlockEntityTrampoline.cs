using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace StovesMod.Blocks
{
/*
* Take a look at https://apidocs.vintagestory.at/api/Vintagestory.API.Common.Block.html#methods and
*   https://apidocs.vintagestory.at/api/Vintagestory.API.Common.CollectibleObject.html#methods for all the methods that can be overriden.
*/
    public class BlockEntityTrampoline : BlockEntityOpenableContainer, IHeatSource, IFirePit, ITemperatureSensitive
    { 
        public bool IsHot
        {
            get
            {
                return this.IsBurning;
            }
        }

        // Token: 0x170001AC RID: 428
        // (get) Token: 0x06000C9F RID: 3231 RVA: 0x00085A49 File Offset: 0x00083C49
        public virtual bool BurnsAllFuell
        {
            get
            {
                return true;
            }
        }

        // Token: 0x170001AD RID: 429
        // (get) Token: 0x06000CA0 RID: 3232 RVA: 0x00085A4C File Offset: 0x00083C4C
        public virtual float HeatModifier
        {
            get
            {
                return 1f;
            }
        }

        // Token: 0x170001AE RID: 430
        // (get) Token: 0x06000CA1 RID: 3233 RVA: 0x00085A53 File Offset: 0x00083C53
        public virtual float BurnDurationModifier
        {
            get
            {
                return 1f;
            }
        }

        // Token: 0x06000CA2 RID: 3234 RVA: 0x00085A5A File Offset: 0x00083C5A
        public virtual int enviromentTemperature()
        {
            return 20;
        }

        // Token: 0x06000CA3 RID: 3235 RVA: 0x00085A60 File Offset: 0x00083C60
        public virtual float maxCookingTime()
        {
            if (this.inputSlot.Itemstack != null)
            {
                return this.inputSlot.Itemstack.Collectible.GetMeltingDuration(this.Api.World, this.inventory, this.inputSlot);
            }
            return 30f;
        }

        // Token: 0x170001AF RID: 431
        // (get) Token: 0x06000CA4 RID: 3236 RVA: 0x00085AAC File Offset: 0x00083CAC
        public override string InventoryClassName
        {
            get
            {
                return "stove";
            }
        }

        // Token: 0x170001B0 RID: 432
        // (get) Token: 0x06000CA5 RID: 3237 RVA: 0x00085AB3 File Offset: 0x00083CB3
        public virtual string DialogTitle
        {
            get
            {
                return Lang.Get("Firepit", Array.Empty<object>());
            }
        }

        // Token: 0x170001B1 RID: 433
        // (get) Token: 0x06000CA6 RID: 3238 RVA: 0x00085AC4 File Offset: 0x00083CC4
        public override InventoryBase Inventory
        {
            get
            {
                return this.inventory;
            }
        }

        // Token: 0x06000CA7 RID: 3239 RVA: 0x00085ACC File Offset: 0x00083CCC
        public BlockEntityTrampoline()
        {
            this.inventory = new InventorySmelting(null, null);
            this.inventory.SlotModified += this.OnSlotModifid;
        }

        // Token: 0x06000CA8 RID: 3240 RVA: 0x00085B24 File Offset: 0x00083D24
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            this.inventory.pos = this.Pos;
            this.inventory.LateInitialize(string.Concat(new string[]
            {
            "smelting-",
            this.Pos.X.ToString(),
            "/",
            this.Pos.Y.ToString(),
            "/",
            this.Pos.Z.ToString()
            }), api);
            this.RegisterGameTickListener(new Action<float>(this.OnBurnTick), 100, 0);
            this.RegisterGameTickListener(new Action<float>(this.On500msTick), 500, 0);
            if (api is ICoreClientAPI)
            {
                this.renderer = new FirepitContentsRendererWithoutInternals(api as ICoreClientAPI, this.Pos);
                (api as ICoreClientAPI).Event.RegisterRenderer(this.renderer, EnumRenderStage.Opaque, "firepit");
                this.UpdateRenderer();
            }
        }

        // Token: 0x06000CA9 RID: 3241 RVA: 0x00085C24 File Offset: 0x00083E24
        private void OnSlotModifid(int slotid)
        {
            base.Block = this.Api.World.BlockAccessor.GetBlock(this.Pos);
            this.UpdateRenderer();
            this.MarkDirty(this.Api.Side == EnumAppSide.Server, null);
            this.shouldRedraw = true;
            if (this.Api is ICoreClientAPI && this.clientDialog != null)
            {
                this.SetDialogValues(this.clientDialog.Attributes);
            }
            IWorldChunk chunkAtBlockPos = this.Api.World.BlockAccessor.GetChunkAtBlockPos(this.Pos);
            if (chunkAtBlockPos == null)
            {
                return;
            }
            chunkAtBlockPos.MarkModified();
        }

        // Token: 0x170001B2 RID: 434
        // (get) Token: 0x06000CAA RID: 3242 RVA: 0x00085CBF File Offset: 0x00083EBF
        public bool IsSmoldering
        {
            get
            {
                return this.canIgniteFuel;
            }
        }

        // Token: 0x170001B3 RID: 435
        // (get) Token: 0x06000CAB RID: 3243 RVA: 0x00085CC7 File Offset: 0x00083EC7
        public bool IsBurning
        {
            get
            {
                return this.fuelBurnTime > 0f;
            }
        }

        // Token: 0x06000CAC RID: 3244 RVA: 0x00085CD6 File Offset: 0x00083ED6
        private void On500msTick(float dt)
        {
            if (this.Api is ICoreServerAPI && (this.IsBurning || this.prevFurnaceTemperature != this.furnaceTemperature))
            {
                this.MarkDirty(false, null);
            }
            this.prevFurnaceTemperature = this.furnaceTemperature;
        }

        // Token: 0x06000CAD RID: 3245 RVA: 0x00085D10 File Offset: 0x00083F10
        private void OnBurnTick(float dt)
        {
            if (base.Block.Code.Path.Contains("construct"))
            {
                return;
            }
            if (!(this.Api is ICoreClientAPI))
            {
                if (this.fuelBurnTime > 0f)
                {
                    bool lowFuelConsumption = Math.Abs(this.furnaceTemperature - (float)this.maxTemperature) < 50f && this.inputSlot.Empty;
                    this.fuelBurnTime -= dt / (lowFuelConsumption ? this.emptyFirepitBurnTimeMulBonus : 1f);
                    if (this.fuelBurnTime <= 0f)
                    {
                        this.fuelBurnTime = 0f;
                        this.maxFuelBurnTime = 0f;
                        if (!this.canSmelt())
                        {
                            this.setBlockState("extinct");
                            this.extinguishedTotalHours = this.Api.World.Calendar.TotalHours;
                        }
                    }
                }
                if (!this.IsBurning && base.Block.Variant["burnstate"] == "extinct" && this.Api.World.Calendar.TotalHours - this.extinguishedTotalHours > 2.0)
                {
                    this.canIgniteFuel = false;
                    this.setBlockState("cold");
                }
                if (this.IsBurning)
                {
                    this.furnaceTemperature = this.changeTemperature(this.furnaceTemperature, (float)this.maxTemperature, dt);
                }
                if (this.canHeatInput())
                {
                    this.heatInput(dt);
                }
                else
                {
                    this.inputStackCookingTime = 0f;
                }
                if (this.canHeatOutput())
                {
                    this.heatOutput(dt);
                }
                if (this.canSmeltInput() && this.inputStackCookingTime > this.maxCookingTime())
                {
                    this.smeltItems();
                }
                if (!this.IsBurning && this.canIgniteFuel && this.canSmelt())
                {
                    this.igniteFuel();
                }
                if (!this.IsBurning)
                {
                    this.furnaceTemperature = this.changeTemperature(this.furnaceTemperature, (float)this.enviromentTemperature(), dt);
                }
                return;
            }
            FirepitContentsRendererWithoutInternals FirepitContentsRendererWithoutInternals = this.renderer;
            if (FirepitContentsRendererWithoutInternals == null)
            {
                return;
            }
            IInFirepitRenderer contentStackRenderer = FirepitContentsRendererWithoutInternals.contentStackRenderer;
            if (contentStackRenderer == null)
            {
                return;
            }
            contentStackRenderer.OnUpdate(this.InputStackTemp);
        }

        // Token: 0x06000CAE RID: 3246 RVA: 0x00085F1C File Offset: 0x0008411C
        public EnumIgniteState GetIgnitableState(float secondsIgniting)
        {
            if (this.fuelSlot.Empty)
            {
                return EnumIgniteState.NotIgnitablePreventDefault;
            }
            if (this.IsBurning)
            {
                return EnumIgniteState.NotIgnitablePreventDefault;
            }
            if (secondsIgniting <= 3f)
            {
                return EnumIgniteState.Ignitable;
            }
            return EnumIgniteState.IgniteNow;
        }

        // Token: 0x06000CAF RID: 3247 RVA: 0x00085F44 File Offset: 0x00084144
        public float changeTemperature(float fromTemp, float toTemp, float dt)
        {
            float diff = Math.Abs(fromTemp - toTemp);
            dt += dt * (diff / 28f);
            if (diff < dt)
            {
                return toTemp;
            }
            if (fromTemp > toTemp)
            {
                dt = -dt;
            }
            if (Math.Abs(fromTemp - toTemp) < 1f)
            {
                return toTemp;
            }
            return fromTemp + dt;
        }

        // Token: 0x06000CB0 RID: 3248 RVA: 0x00085F8C File Offset: 0x0008418C
        private bool canSmelt()
        {
            CombustibleProperties fuelCopts = this.fuelCombustibleOpts;
            if (fuelCopts == null)
            {
                return false;
            }
            bool smeltableInput = this.canHeatInput();
            return (this.BurnsAllFuell || smeltableInput) && (float)fuelCopts.BurnTemperature * this.HeatModifier > 0f;
        }

        // Token: 0x06000CB1 RID: 3249 RVA: 0x00085FD0 File Offset: 0x000841D0
        public void heatInput(float dt)
        {
            float oldTemp = this.InputStackTemp;
            float nowTemp = oldTemp;
            float meltingPoint = this.inputSlot.Itemstack.Collectible.GetMeltingPoint(this.Api.World, this.inventory, this.inputSlot);
            if (oldTemp < this.furnaceTemperature)
            {
                float f = (1f + GameMath.Clamp((this.furnaceTemperature - oldTemp) / 30f, 0f, 1.6f)) * dt;
                if (nowTemp >= meltingPoint)
                {
                    f /= 11f;
                }
                float newTemp = this.changeTemperature(oldTemp, this.furnaceTemperature, f);
                int val = (this.inputStack.Collectible.CombustibleProps == null) ? 0 : this.inputStack.Collectible.CombustibleProps.MaxTemperature;
                JsonObject itemAttributes = this.inputStack.ItemAttributes;
                int maxTemp = Math.Max(val, (((itemAttributes != null) ? itemAttributes["maxTemperature"] : null) == null) ? 0 : this.inputStack.ItemAttributes["maxTemperature"].AsInt(0));
                if (maxTemp > 0)
                {
                    newTemp = Math.Min((float)maxTemp, newTemp);
                }
                if (oldTemp != newTemp)
                {
                    this.InputStackTemp = newTemp;
                    nowTemp = newTemp;
                }
            }
            if (nowTemp >= meltingPoint)
            {
                float diff = nowTemp / meltingPoint;
                this.inputStackCookingTime += (float)GameMath.Clamp((int)diff, 1, 30) * dt;
                return;
            }
            if (this.inputStackCookingTime > 0f)
            {
                this.inputStackCookingTime -= 1f;
            }
        }

        // Token: 0x06000CB2 RID: 3250 RVA: 0x00086134 File Offset: 0x00084334
        public void heatOutput(float dt)
        {
            float oldTemp = this.OutputStackTemp;
            if (oldTemp < this.furnaceTemperature)
            {
                float newTemp = this.changeTemperature(oldTemp, this.furnaceTemperature, 2f * dt);
                int val = (this.outputStack.Collectible.CombustibleProps == null) ? 0 : this.outputStack.Collectible.CombustibleProps.MaxTemperature;
                JsonObject itemAttributes = this.outputStack.ItemAttributes;
                int maxTemp = Math.Max(val, (((itemAttributes != null) ? itemAttributes["maxTemperature"] : null) == null) ? 0 : this.outputStack.ItemAttributes["maxTemperature"].AsInt(0));
                if (maxTemp > 0)
                {
                    newTemp = Math.Min((float)maxTemp, newTemp);
                }
                if (oldTemp != newTemp)
                {
                    this.OutputStackTemp = newTemp;
                }
            }
        }

        // Token: 0x06000CB3 RID: 3251 RVA: 0x000861F0 File Offset: 0x000843F0
        public void CoolNow(float amountRel)
        {
            this.Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), this.Pos, -0.5, null, false, 16f, 1f);
            this.fuelBurnTime -= amountRel / 10f;
            if (this.Api.World.Rand.NextDouble() < (double)(amountRel / 5f) || this.fuelBurnTime <= 0f)
            {
                this.setBlockState("cold");
                this.extinguishedTotalHours = -99.0;
                this.canIgniteFuel = false;
                this.fuelBurnTime = 0f;
                this.maxFuelBurnTime = 0f;
            }
            this.MarkDirty(true, null);
        }

        // Token: 0x170001B4 RID: 436
        // (get) Token: 0x06000CB4 RID: 3252 RVA: 0x000862B2 File Offset: 0x000844B2
        // (set) Token: 0x06000CB5 RID: 3253 RVA: 0x000862C0 File Offset: 0x000844C0
        public float InputStackTemp
        {
            get
            {
                return this.GetTemp(this.inputStack);
            }
            set
            {
                this.SetTemp(this.inputStack, value);
            }
        }

        // Token: 0x170001B5 RID: 437
        // (get) Token: 0x06000CB6 RID: 3254 RVA: 0x000862CF File Offset: 0x000844CF
        // (set) Token: 0x06000CB7 RID: 3255 RVA: 0x000862DD File Offset: 0x000844DD
        public float OutputStackTemp
        {
            get
            {
                return this.GetTemp(this.outputStack);
            }
            set
            {
                this.SetTemp(this.outputStack, value);
            }
        }

        // Token: 0x06000CB8 RID: 3256 RVA: 0x000862EC File Offset: 0x000844EC
        private float GetTemp(ItemStack stack)
        {
            if (stack == null)
            {
                return (float)this.enviromentTemperature();
            }
            if (this.inventory.CookingSlots.Length != 0)
            {
                bool haveStack = false;
                float lowestTemp = 0f;
                for (int i = 0; i < this.inventory.CookingSlots.Length; i++)
                {
                    ItemStack cookingStack = this.inventory.CookingSlots[i].Itemstack;
                    if (cookingStack != null)
                    {
                        float stackTemp = cookingStack.Collectible.GetTemperature(this.Api.World, cookingStack);
                        lowestTemp = (haveStack ? Math.Min(lowestTemp, stackTemp) : stackTemp);
                        haveStack = true;
                    }
                }
                return lowestTemp;
            }
            return stack.Collectible.GetTemperature(this.Api.World, stack);
        }

        // Token: 0x06000CB9 RID: 3257 RVA: 0x0008638C File Offset: 0x0008458C
        private void SetTemp(ItemStack stack, float value)
        {
            if (stack == null)
            {
                return;
            }
            if (this.inventory.CookingSlots.Length != 0)
            {
                for (int i = 0; i < this.inventory.CookingSlots.Length; i++)
                {
                    ItemStack itemstack = this.inventory.CookingSlots[i].Itemstack;
                    if (itemstack != null)
                    {
                        itemstack.Collectible.SetTemperature(this.Api.World, this.inventory.CookingSlots[i].Itemstack, value, true);
                    }
                }
                return;
            }
            stack.Collectible.SetTemperature(this.Api.World, stack, value, true);
        }

        // Token: 0x06000CBA RID: 3258 RVA: 0x0008641E File Offset: 0x0008461E
        public void igniteFuel()
        {
            this.igniteWithFuel(this.fuelStack);
            this.fuelStack.StackSize--;
            if (this.fuelStack.StackSize <= 0)
            {
                this.fuelStack = null;
            }
        }

        // Token: 0x06000CBB RID: 3259 RVA: 0x00086454 File Offset: 0x00084654
        public void igniteWithFuel(IItemStack stack)
        {
            CombustibleProperties fuelCopts = stack.Collectible.CombustibleProps;
            this.maxFuelBurnTime = (this.fuelBurnTime = fuelCopts.BurnDuration * this.BurnDurationModifier);
            this.maxTemperature = (int)((float)fuelCopts.BurnTemperature * this.HeatModifier);
            this.smokeLevel = fuelCopts.SmokeLevel;
            this.setBlockState("lit");
            this.MarkDirty(true, null);
        }

        // Token: 0x06000CBC RID: 3260 RVA: 0x000864C0 File Offset: 0x000846C0
        public void setBlockState(string state)
        {
            AssetLocation loc = base.Block.CodeWithVariant("burnstate", state);
            Block block = this.Api.World.GetBlock(loc);
            if (block == null)
            {
                return;
            }
            this.Api.World.BlockAccessor.ExchangeBlock(block.Id, this.Pos);
            base.Block = block;
        }

        // Token: 0x06000CBD RID: 3261 RVA: 0x00086520 File Offset: 0x00084720
        public bool canHeatInput()
        {
            if (!this.canSmeltInput())
            {
                ItemStack inputStack = this.inputStack;
                bool flag;
                if (inputStack == null)
                {
                    flag = (null != null);
                }
                else
                {
                    JsonObject itemAttributes = inputStack.ItemAttributes;
                    flag = (((itemAttributes != null) ? itemAttributes["allowHeating"] : null) != null);
                }
                return flag && this.inputStack.ItemAttributes["allowHeating"].AsBool(false);
            }
            return true;
        }

        // Token: 0x06000CBE RID: 3262 RVA: 0x0008657C File Offset: 0x0008477C
        public bool canHeatOutput()
        {
            ItemStack outputStack = this.outputStack;
            bool flag;
            if (outputStack == null)
            {
                flag = (null != null);
            }
            else
            {
                JsonObject itemAttributes = outputStack.ItemAttributes;
                flag = (((itemAttributes != null) ? itemAttributes["allowHeating"] : null) != null);
            }
            return flag && this.outputStack.ItemAttributes["allowHeating"].AsBool(false);
        }

        // Token: 0x06000CBF RID: 3263 RVA: 0x000865CC File Offset: 0x000847CC
        public bool canSmeltInput()
        {
            if (this.inputStack == null)
            {
                return false;
            }
            if (this.inputStack.Collectible.OnSmeltAttempt(this.inventory))
            {
                this.MarkDirty(true, null);
            }
            return this.inputStack.Collectible.CanSmelt(this.Api.World, this.inventory, this.inputSlot.Itemstack, this.outputSlot.Itemstack) && (this.inputStack.Collectible.CombustibleProps == null || !this.inputStack.Collectible.CombustibleProps.RequiresContainer);
        }

        // Token: 0x06000CC0 RID: 3264 RVA: 0x0008666C File Offset: 0x0008486C
        public void smeltItems()
        {
            this.inputStack.Collectible.DoSmelt(this.Api.World, this.inventory, this.inputSlot, this.outputSlot);
            this.InputStackTemp = (float)this.enviromentTemperature();
            this.inputStackCookingTime = 0f;
            this.MarkDirty(true, null);
            this.inputSlot.MarkDirty();
        }

        // Token: 0x06000CC1 RID: 3265 RVA: 0x000866D1 File Offset: 0x000848D1
        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (this.Api.Side == EnumAppSide.Client)
            {
                base.toggleInventoryDialogClient(byPlayer, delegate
                {
                    SyncedTreeAttribute dtree = new SyncedTreeAttribute();
                    this.SetDialogValues(dtree);
                    this.clientDialog = new GuiDialogBlockEntityFirepit(this.DialogTitle, this.Inventory, this.Pos, dtree, this.Api as ICoreClientAPI);
                    return this.clientDialog;
                });
            }
            return true;
        }

        // Token: 0x06000CC2 RID: 3266 RVA: 0x000866F5 File Offset: 0x000848F5
        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(player, packetid, data);
        }

        // Token: 0x06000CC3 RID: 3267 RVA: 0x00086700 File Offset: 0x00084900
        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            if (packetid == 1001)
            {
                (this.Api.World as IClientWorldAccessor).Player.InventoryManager.CloseInventory(this.Inventory);
                GuiDialogBlockEntity invDialog = this.invDialog;
                if (invDialog != null)
                {
                    invDialog.TryClose();
                }
                GuiDialogBlockEntity invDialog2 = this.invDialog;
                if (invDialog2 != null)
                {
                    invDialog2.Dispose();
                }
                this.invDialog = null;
            }
        }

        // Token: 0x06000CC4 RID: 3268 RVA: 0x00086768 File Offset: 0x00084968
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            if (this.Api != null)
            {
                this.Inventory.AfterBlocksLoaded(this.Api.World);
            }
            this.furnaceTemperature = tree.GetFloat("furnaceTemperature", 0f);
            this.maxTemperature = tree.GetInt("maxTemperature", 0);
            this.inputStackCookingTime = tree.GetFloat("oreCookingTime", 0f);
            this.fuelBurnTime = tree.GetFloat("fuelBurnTime", 0f);
            this.maxFuelBurnTime = tree.GetFloat("maxFuelBurnTime", 0f);
            this.extinguishedTotalHours = tree.GetDouble("extinguishedTotalHours", 0.0);
            this.canIgniteFuel = tree.GetBool("canIgniteFuel", true);
            this.cachedFuel = tree.GetFloat("cachedFuel", 0f);
            ICoreAPI api = this.Api;
            if (api != null && api.Side == EnumAppSide.Client)
            {
                this.UpdateRenderer();
                if (this.clientDialog != null)
                {
                    this.SetDialogValues(this.clientDialog.Attributes);
                }
            }
            ICoreAPI api2 = this.Api;
            if (api2 != null && api2.Side == EnumAppSide.Client && (this.clientSidePrevBurning != this.IsBurning || this.shouldRedraw))
            {
                BEBehaviorFirepitAmbient behavior = base.GetBehavior<BEBehaviorFirepitAmbient>();
                if (behavior != null)
                {
                    behavior.ToggleAmbientSounds(this.IsBurning);
                }
                this.clientSidePrevBurning = this.IsBurning;
                this.MarkDirty(true, null);
                this.shouldRedraw = false;
            }
        }

        // Token: 0x06000CC5 RID: 3269 RVA: 0x000868DC File Offset: 0x00084ADC
        private void UpdateRenderer()
        {
            if (this.renderer == null)
            {
                return;
            }
            ItemStack contentStack = (this.inputStack == null) ? this.outputStack : this.inputStack;
            if (this.renderer.ContentStack != null && this.renderer.contentStackRenderer != null && ((contentStack != null) ? contentStack.Collectible : null) is IInFirepitRendererSupplier && this.renderer.ContentStack.Equals(this.Api.World, contentStack, GlobalConstants.IgnoredStackAttributes))
            {
                return;
            }
            IInFirepitRenderer contentStackRenderer = this.renderer.contentStackRenderer;
            if (contentStackRenderer != null)
            {
                contentStackRenderer.Dispose();
            }
            this.renderer.contentStackRenderer = null;
            if (((contentStack != null) ? contentStack.Collectible : null) is IInFirepitRendererSupplier)
            {
                IInFirepitRenderer childrenderer = (((contentStack != null) ? contentStack.Collectible : null) as IInFirepitRendererSupplier).GetRendererWhenInFirepit(contentStack, new BlockEntityFirepit(), contentStack == this.outputStack);
                if (childrenderer != null)
                {
                    this.renderer.SetChildRenderer(contentStack, childrenderer);
                    return;
                }
            }
            InFirePitProps props = this.GetRenderProps(contentStack);
            if (((contentStack != null) ? contentStack.Collectible : null) != null && !(((contentStack != null) ? contentStack.Collectible : null) is IInFirepitMeshSupplier) && props != null)
            {
                this.renderer.SetContents(contentStack, props.Transform);
                return;
            }
            this.renderer.SetContents(null, null);
        }

        // Token: 0x06000CC6 RID: 3270 RVA: 0x00086A14 File Offset: 0x00084C14
        private void SetDialogValues(ITreeAttribute dialogTree)
        {
            dialogTree.SetFloat("furnaceTemperature", this.furnaceTemperature);
            dialogTree.SetInt("maxTemperature", this.maxTemperature);
            dialogTree.SetFloat("oreCookingTime", this.inputStackCookingTime);
            dialogTree.SetFloat("maxFuelBurnTime", this.maxFuelBurnTime);
            dialogTree.SetFloat("fuelBurnTime", this.fuelBurnTime);
            if (this.inputSlot.Itemstack != null)
            {
                float meltingDuration = this.inputSlot.Itemstack.Collectible.GetMeltingDuration(this.Api.World, this.inventory, this.inputSlot);
                dialogTree.SetFloat("oreTemperature", this.InputStackTemp);
                dialogTree.SetFloat("maxOreCookingTime", meltingDuration);
            }
            else
            {
                dialogTree.RemoveAttribute("oreTemperature");
            }
            dialogTree.SetString("outputText", this.inventory.GetOutputText());
            dialogTree.SetInt("haveCookingContainer", this.inventory.HaveCookingContainer ? 1 : 0);
            dialogTree.SetInt("quantityCookingSlots", this.inventory.CookingSlots.Length);
        }

        // Token: 0x06000CC7 RID: 3271 RVA: 0x00086B24 File Offset: 0x00084D24
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            this.Inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;
            tree.SetFloat("furnaceTemperature", this.furnaceTemperature);
            tree.SetInt("maxTemperature", this.maxTemperature);
            tree.SetFloat("oreCookingTime", this.inputStackCookingTime);
            tree.SetFloat("fuelBurnTime", this.fuelBurnTime);
            tree.SetFloat("maxFuelBurnTime", this.maxFuelBurnTime);
            tree.SetDouble("extinguishedTotalHours", this.extinguishedTotalHours);
            tree.SetBool("canIgniteFuel", this.canIgniteFuel);
            tree.SetFloat("cachedFuel", this.cachedFuel);
        }

        // Token: 0x06000CC8 RID: 3272 RVA: 0x00086BE0 File Offset: 0x00084DE0
        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            FirepitContentsRendererWithoutInternals FirepitContentsRendererWithoutInternals = this.renderer;
            if (FirepitContentsRendererWithoutInternals != null)
            {
                FirepitContentsRendererWithoutInternals.Dispose();
            }
            this.renderer = null;
            if (this.clientDialog != null)
            {
                this.clientDialog.TryClose();
                GuiDialogBlockEntityFirepit guiDialogBlockTrampoline = this.clientDialog;
                if (guiDialogBlockTrampoline != null)
                {
                    guiDialogBlockTrampoline.Dispose();
                }
                this.clientDialog = null;
            }
        }

        // Token: 0x170001B6 RID: 438
        // (get) Token: 0x06000CC9 RID: 3273 RVA: 0x00086C37 File Offset: 0x00084E37
        public ItemSlot fuelSlot
        {
            get
            {
                return this.inventory[0];
            }
        }

        // Token: 0x170001B7 RID: 439
        // (get) Token: 0x06000CCA RID: 3274 RVA: 0x00086C45 File Offset: 0x00084E45
        public ItemSlot inputSlot
        {
            get
            {
                return this.inventory[1];
            }
        }

        // Token: 0x170001B8 RID: 440
        // (get) Token: 0x06000CCB RID: 3275 RVA: 0x00086C53 File Offset: 0x00084E53
        public ItemSlot outputSlot
        {
            get
            {
                return this.inventory[2];
            }
        }

        // Token: 0x170001B9 RID: 441
        // (get) Token: 0x06000CCC RID: 3276 RVA: 0x00086C61 File Offset: 0x00084E61
        public ItemSlot[] otherCookingSlots
        {
            get
            {
                return this.inventory.CookingSlots;
            }
        }

        // Token: 0x170001BA RID: 442
        // (get) Token: 0x06000CCD RID: 3277 RVA: 0x00086C6E File Offset: 0x00084E6E
        // (set) Token: 0x06000CCE RID: 3278 RVA: 0x00086C81 File Offset: 0x00084E81
        public ItemStack fuelStack
        {
            get
            {
                return this.inventory[0].Itemstack;
            }
            set
            {
                this.inventory[0].Itemstack = value;
                this.inventory[0].MarkDirty();
            }
        }

        // Token: 0x170001BB RID: 443
        // (get) Token: 0x06000CCF RID: 3279 RVA: 0x00086CA6 File Offset: 0x00084EA6
        // (set) Token: 0x06000CD0 RID: 3280 RVA: 0x00086CB9 File Offset: 0x00084EB9
        public ItemStack inputStack
        {
            get
            {
                return this.inventory[1].Itemstack;
            }
            set
            {
                this.inventory[1].Itemstack = value;
                this.inventory[1].MarkDirty();
            }
        }

        // Token: 0x170001BC RID: 444
        // (get) Token: 0x06000CD1 RID: 3281 RVA: 0x00086CDE File Offset: 0x00084EDE
        // (set) Token: 0x06000CD2 RID: 3282 RVA: 0x00086CF1 File Offset: 0x00084EF1
        public ItemStack outputStack
        {
            get
            {
                return this.inventory[2].Itemstack;
            }
            set
            {
                this.inventory[2].Itemstack = value;
                this.inventory[2].MarkDirty();
            }
        }

        // Token: 0x170001BD RID: 445
        // (get) Token: 0x06000CD3 RID: 3283 RVA: 0x00086D16 File Offset: 0x00084F16
        public CombustibleProperties fuelCombustibleOpts
        {
            get
            {
                return this.getCombustibleOpts(0);
            }
        }

        // Token: 0x06000CD4 RID: 3284 RVA: 0x00086D20 File Offset: 0x00084F20
        public CombustibleProperties getCombustibleOpts(int slotid)
        {
            ItemSlot slot = this.inventory[slotid];
            if (slot.Itemstack == null)
            {
                return null;
            }
            return slot.Itemstack.Collectible.CombustibleProps;
        }

        // Token: 0x06000CD5 RID: 3285 RVA: 0x00086D54 File Offset: 0x00084F54
        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            foreach (ItemSlot slot in this.Inventory)
            {
                if (slot.Itemstack != null)
                {
                    if (slot.Itemstack.Class == EnumItemClass.Item)
                    {
                        itemIdMapping[slot.Itemstack.Item.Id] = slot.Itemstack.Item.Code;
                    }
                    else
                    {
                        blockIdMapping[slot.Itemstack.Block.BlockId] = slot.Itemstack.Block.Code;
                    }
                    slot.Itemstack.Collectible.OnStoreCollectibleMappings(this.Api.World, slot, blockIdMapping, itemIdMapping);
                }
            }
            foreach (ItemSlot slot2 in this.inventory.CookingSlots)
            {
                if (slot2.Itemstack != null)
                {
                    if (slot2.Itemstack.Class == EnumItemClass.Item)
                    {
                        itemIdMapping[slot2.Itemstack.Item.Id] = slot2.Itemstack.Item.Code;
                    }
                    else
                    {
                        blockIdMapping[slot2.Itemstack.Block.BlockId] = slot2.Itemstack.Block.Code;
                    }
                    slot2.Itemstack.Collectible.OnStoreCollectibleMappings(this.Api.World, slot2, blockIdMapping, itemIdMapping);
                }
            }
        }

        // Token: 0x06000CD6 RID: 3286 RVA: 0x00086ED4 File Offset: 0x000850D4
        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForResolve, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
        }

        // Token: 0x170001BE RID: 446
        // (get) Token: 0x06000CD7 RID: 3287 RVA: 0x00086EE3 File Offset: 0x000850E3
        // (set) Token: 0x06000CD8 RID: 3288 RVA: 0x00086EEB File Offset: 0x000850EB
        public EnumFirepitModel CurrentModel { get; private set; }

        // Token: 0x06000CD9 RID: 3289 RVA: 0x00086EF4 File Offset: 0x000850F4
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (base.Block == null || base.Block.Code.Path.Contains("construct"))
            {
                return false;
            }
            ItemStack contentStack = (this.inputStack == null) ? this.outputStack : this.inputStack;
            MeshData contentmesh = this.getContentMesh(contentStack, tesselator);
            if (contentmesh != null)
            {
                mesher.AddMeshData(contentmesh, 1);
            }
            string burnState = base.Block.Variant["burnstate"];
            string contentState = this.CurrentModel.ToString().ToLowerInvariant();
            if (burnState == "cold" && this.fuelSlot.Empty)
            {
                burnState = "extinct";
            }
            if (burnState == null)
            {
                return true;
            }
            mesher.AddMeshData(this.getOrCreateMesh(burnState, contentState), 1);
            return true;
        }

        // Token: 0x06000CDA RID: 3290 RVA: 0x00086FB8 File Offset: 0x000851B8
        private MeshData getContentMesh(ItemStack contentStack, ITesselatorAPI tesselator)
        {
            this.CurrentModel = EnumFirepitModel.Normal;
            if (contentStack == null)
            {
                return null;
            }
            if (contentStack.Collectible is IInFirepitMeshSupplier)
            {
                EnumFirepitModel model = EnumFirepitModel.Normal;
                MeshData mesh = (contentStack.Collectible as IInFirepitMeshSupplier).GetMeshWhenInFirepit(contentStack, this.Api.World, this.Pos, ref model);
                this.CurrentModel = model;
                if (mesh != null)
                {
                    return mesh;
                }
            }
            if (contentStack.Collectible is IInFirepitRendererSupplier)
            {
                EnumFirepitModel model2 = (contentStack.Collectible as IInFirepitRendererSupplier).GetDesiredFirepitModel(contentStack, new BlockEntityFirepit(), contentStack == this.outputStack);
                this.CurrentModel = model2;
                return null;
            }
            InFirePitProps renderProps = this.GetRenderProps(contentStack);
            if (renderProps == null)
            {
                if (this.renderer.RequireSpit)
                {
                    this.CurrentModel = EnumFirepitModel.Spit;
                }
                return null;
            }
            this.CurrentModel = renderProps.UseFirepitModel;
            if (contentStack.Class != EnumItemClass.Item)
            {
                MeshData ingredientMesh;
                tesselator.TesselateBlock(contentStack.Block, out ingredientMesh);
                ingredientMesh.ModelTransform(renderProps.Transform);
                if (!this.IsBurning && renderProps.UseFirepitModel != EnumFirepitModel.Spit)
                {
                    ingredientMesh.Translate(0f, -0.0625f, 0f);
                }
                return ingredientMesh;
            }
            return null;
        }

        // Token: 0x06000CDB RID: 3291 RVA: 0x000870C1 File Offset: 0x000852C1
        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            FirepitContentsRendererWithoutInternals FirepitContentsRendererWithoutInternals = this.renderer;
            if (FirepitContentsRendererWithoutInternals == null)
            {
                return;
            }
            FirepitContentsRendererWithoutInternals.Dispose();
        }

        // Token: 0x06000CDC RID: 3292 RVA: 0x000870DC File Offset: 0x000852DC
        private InFirePitProps GetRenderProps(ItemStack contentStack)
        {
            if (contentStack != null)
            {
                JsonObject itemAttributes = contentStack.ItemAttributes;
                if (((itemAttributes != null) ? new bool?(itemAttributes.KeyExists("inFirePitProps")) : null).GetValueOrDefault())
                {
                    InFirePitProps inFirePitProps = contentStack.ItemAttributes["inFirePitProps"].AsObject<InFirePitProps>(null);
                    inFirePitProps.Transform.EnsureDefaultValues();
                    return inFirePitProps;
                }
            }
            return null;
        }

        // Token: 0x06000CDD RID: 3293 RVA: 0x00087140 File Offset: 0x00085340
        public MeshData getOrCreateMesh(string burnstate, string contentstate)
        {
            Dictionary<string, MeshData> orCreate = ObjectCacheUtil.GetOrCreate<Dictionary<string, MeshData>>(this.Api, "firepit-meshes", () => new Dictionary<string, MeshData>());
            string key = burnstate + "-" + contentstate;
            MeshData meshdata;
            if (!orCreate.TryGetValue(key, out meshdata))
            {
                Block block = this.Api.World.BlockAccessor.GetBlock(this.Pos);
                if (block.BlockId == 0)
                {
                    return null;
                }
                //new MeshData[17];
                ((ICoreClientAPI)this.Api).Tesselator.TesselateShape(block, Shape.TryGet(this.Api, "shapes/block/wood/firepit/" + key + ".json"), out meshdata, null, null, null);
            }
            return meshdata;
        }

        // Token: 0x06000CDE RID: 3294 RVA: 0x00087200 File Offset: 0x00085400
        public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos)
        {
            if (this.IsBurning)
            {
                return 10f;
            }
            if (!this.IsSmoldering)
            {
                return 0f;
            }
            return 0.25f;
        }

        // Token: 0x040008D2 RID: 2258
        internal InventorySmelting inventory;

        // Token: 0x040008D3 RID: 2259
        public float prevFurnaceTemperature = 20f;

        // Token: 0x040008D4 RID: 2260
        public float furnaceTemperature = 20f;

        // Token: 0x040008D5 RID: 2261
        public int maxTemperature;

        // Token: 0x040008D6 RID: 2262
        public float inputStackCookingTime;

        // Token: 0x040008D7 RID: 2263
        public float fuelBurnTime;

        // Token: 0x040008D8 RID: 2264
        public float maxFuelBurnTime;

        // Token: 0x040008D9 RID: 2265
        public float smokeLevel;

        // Token: 0x040008DA RID: 2266
        public bool canIgniteFuel;

        // Token: 0x040008DB RID: 2267
        public float cachedFuel;

        // Token: 0x040008DC RID: 2268
        public double extinguishedTotalHours;

        // Token: 0x040008DD RID: 2269
        private GuiDialogBlockEntityFirepit clientDialog;

        // Token: 0x040008DE RID: 2270
        private bool clientSidePrevBurning;

        // Token: 0x040008DF RID: 2271
        private FirepitContentsRendererWithoutInternals renderer;

        // Token: 0x040008E0 RID: 2272
        private bool shouldRedraw;

        // Token: 0x040008E1 RID: 2273
        public float emptyFirepitBurnTimeMulBonus = 4f;
    }
}
