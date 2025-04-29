﻿using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace StovesMod.Blocks;

public class FirepitContentsRendererWithoutInternals : IRenderer, IDisposable
{
    private MultiTextureMeshRef meshref;

    private ICoreClientAPI api;

    private BlockPos pos;

    public ItemStack ContentStack;

    private int textureId;

    private Matrixf ModelMat = new Matrixf();

    private ModelTransform transform;

    private ModelTransform defaultTransform;

    public IInFirepitRenderer contentStackRenderer;

    public bool RequireSpit
    {
        get
        {
            if (contentStackRenderer == null)
            {
                return ContentStack?.Item != null;
            }

            return false;
        }
    }

    public double RenderOrder => 0.5;

    public int RenderRange => 48;

    public FirepitContentsRendererWithoutInternals(ICoreClientAPI api, BlockPos pos)
    {
        this.api = api;
        this.pos = pos;
        transform = new ModelTransform().EnsureDefaultValues();
        transform.Origin.X = 0.5f;
        transform.Origin.Y = 0.0625f;
        transform.Origin.Z = 0.5f;
        transform.Rotation.X = 90f;
        transform.Rotation.Y = 90f;
        transform.Rotation.Z = 0f;
        transform.Translation.X = 0f;
        transform.Translation.Y = 0.25f;
        transform.Translation.Z = 0f;
        transform.ScaleXYZ.X = 0.25f;
        transform.ScaleXYZ.Y = 0.25f;
        transform.ScaleXYZ.Z = 0.25f;
        defaultTransform = transform;
    }

    public void SetChildRenderer(ItemStack contentStack, IInFirepitRenderer renderer)
    {
        ContentStack = contentStack;
        meshref?.Dispose();
        meshref = null;
        contentStackRenderer = renderer;
    }

    public void SetContents(ItemStack newContentStack, ModelTransform transform)
    {
        contentStackRenderer?.Dispose();
        contentStackRenderer = null;
        this.transform = transform;
        if (transform == null)
        {
            this.transform = defaultTransform;
        }

        this.transform.EnsureDefaultValues();
        meshref?.Dispose();
        meshref = null;
        if (newContentStack == null || newContentStack.Class == EnumItemClass.Block)
        {
            ContentStack = null;
            return;
        }

        MeshData modeldata;
        if (newContentStack.Class == EnumItemClass.Item)
        {
            api.Tesselator.TesselateItem(newContentStack.Item, out modeldata);
            textureId = api.ItemTextureAtlas.Positions[newContentStack.Item.FirstTexture.Baked.TextureSubId].atlasTextureId;
        }
        else
        {
            api.Tesselator.TesselateBlock(newContentStack.Block, out modeldata);
            textureId = api.ItemTextureAtlas.Positions[newContentStack.Block.Textures.FirstOrDefault().Value.Baked.TextureSubId].atlasTextureId;
        }

        meshref = api.Render.UploadMultiTextureMesh(modeldata);
        ContentStack = newContentStack;
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (contentStackRenderer != null)
        {
            contentStackRenderer.OnRenderFrame(deltaTime, stage);
        }
        else if (meshref != null)
        {
            IRenderAPI render = api.Render;
            Vec3d cameraPos = api.World.Player.Entity.CameraPos;
            render.GlDisableCullFace();
            render.GlToggleBlend(blend: true);
            IStandardShaderProgram standardShader = render.StandardShader;
            standardShader.Use();
            standardShader.DontWarpVertices = 0;
            standardShader.AddRenderFlags = 0;
            standardShader.RgbaAmbientIn = render.AmbientColor;
            standardShader.RgbaFogIn = render.FogColor;
            standardShader.FogMinIn = render.FogMin;
            standardShader.FogDensityIn = render.FogDensity;
            standardShader.RgbaTint = ColorUtil.WhiteArgbVec;
            standardShader.NormalShaded = 1;
            standardShader.ExtraGodray = 0f;
            standardShader.SsaoAttn = 0f;
            standardShader.AlphaTest = 0.05f;
            standardShader.OverlayOpacity = 0f;
            int num = (int)ContentStack.Collectible.GetTemperature(api.World, ContentStack);
            Vec4f lightRGBs = api.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
            float[] incandescenceColorAsColor4f = ColorUtil.GetIncandescenceColorAsColor4f(num);
            lightRGBs[0] += incandescenceColorAsColor4f[0];
            lightRGBs[1] += incandescenceColorAsColor4f[1];
            lightRGBs[2] += incandescenceColorAsColor4f[2];
            standardShader.RgbaLightIn = lightRGBs;
            standardShader.ExtraGlow = GameMath.Clamp((num - 500) / 4, 0, 255);
            standardShader.ModelMatrix = ModelMat.Identity().Translate((double)pos.X - cameraPos.X + (double)transform.Translation.X, (double)pos.Y - cameraPos.Y + (double)transform.Translation.Y, (double)pos.Z - cameraPos.Z + (double)transform.Translation.Z).Translate(transform.Origin.X, 0.6f + transform.Origin.Y, transform.Origin.Z)
                .RotateX(transform.Rotation.X * (MathF.PI / 180f))
                .RotateY(transform.Rotation.Y * (MathF.PI / 180f))
                .RotateZ(transform.Rotation.Z * (MathF.PI / 180f))
                .Scale(transform.ScaleXYZ.X, transform.ScaleXYZ.Y, transform.ScaleXYZ.Z)
                .Translate(0f - transform.Origin.X, 0f - transform.Origin.Y, 0f - transform.Origin.Z)
                .Values;
            standardShader.ViewMatrix = render.CameraMatrixOriginf;
            standardShader.ProjectionMatrix = render.CurrentProjectionMatrix;
            render.RenderMultiTextureMesh(meshref, "tex");
            standardShader.Stop();
        }
    }

    public void Dispose()
    {
        api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        meshref?.Dispose();
        contentStackRenderer?.Dispose();
    }
}
