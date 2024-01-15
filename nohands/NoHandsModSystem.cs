using System.Reflection;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using System.Collections.Generic;
using Vintagestory.API.Common.Entities;
using HarmonyLib;

namespace NoHands;
public class NoHandsModSystem : ModSystem
{
    static private ICoreClientAPI mApi;

    public override void StartClientSide(ICoreClientAPI api)
    {
        mApi = api;

        new Harmony("nohands").Patch(
                    AccessTools.Method(typeof(EntityPlayerShapeRenderer), nameof(EntityPlayerShapeRenderer.Tesselate)),
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(NoHandsModSystem), nameof(Tesselate)))
                );
    }

    public override void Dispose()
    {
        new Harmony("nohands").Unpatch(AccessTools.Method(typeof(EntityPlayerShapeRenderer), nameof(EntityPlayerShapeRenderer.Tesselate)), HarmonyPatchType.Prefix, "nohands");
    }

    static public bool Tesselate(EntityPlayerShapeRenderer __instance)
    {
        Property<bool, EntitySkinnableShapeRenderer> IsSelf = new(typeof(EntitySkinnableShapeRenderer), "IsSelf", __instance);
        Field<bool, EntityShapeRenderer> loaded = new(typeof(EntityShapeRenderer), "loaded", __instance);
        Field<bool, EntityShapeRenderer> shapeFresh = new(typeof(EntityShapeRenderer), "shapeFresh", __instance);

        if (!IsSelf.Value)
        {
            return true;
        }

        if (!loaded.Value) return false;

        shapeFresh.Value = true;
        __instance.TesselateShape(meshData => DelegateAction(__instance, meshData, mApi));

        return false;
    }
    static public void DelegateAction(EntityPlayerShapeRenderer __instance, MeshData meshData, ICoreClientAPI capi)
    {
        Field<Entity, EntityRenderer> entity = new(typeof(EntityRenderer), "entity", __instance);
        Field<MultiTextureMeshRef, EntityPlayerShapeRenderer> firstPersonMeshRef = new(typeof(EntityPlayerShapeRenderer), "firstPersonMeshRef", __instance);
        Field<MultiTextureMeshRef, EntityPlayerShapeRenderer> thirdPersonMeshRef = new(typeof(EntityPlayerShapeRenderer), "thirdPersonMeshRef", __instance);

        DisposeMeshes(__instance);
        if (!capi.IsShuttingDown && meshData.VerticesCount > 0)
        {
            MeshData meshData2 = meshData.EmptyClone();
            thirdPersonMeshRef.Value = capi.Render.UploadMultiTextureMesh(meshData);
            if (capi.Settings.Bool["immersiveFpMode"])
            {
                HashSet<int> skipJointIds = new HashSet<int>();
                loadJointIdsRecursive(entity.Value.AnimManager.Animator.GetPosebyName("Neck"), skipJointIds);
                meshData2.AddMeshData(meshData, (int i) => !skipJointIds.Contains(meshData.CustomInts.Values[i * 4]));
            }
            else
            {
                HashSet<int> includeJointIds = new HashSet<int>();
                loadJointIdsRecursive(entity.Value.AnimManager.Animator.GetPosebyName("ItemAnchor"), includeJointIds);
                meshData2.AddMeshData(meshData, (int i) => includeJointIds.Contains(meshData.CustomInts.Values[i * 4]));
            }

            firstPersonMeshRef.Value = capi.Render.UploadMultiTextureMesh(meshData2);
        }
    }

    static private void DisposeMeshes(EntityPlayerShapeRenderer __instance)
    {
        Field<MultiTextureMeshRef, EntityPlayerShapeRenderer> firstPersonMeshRef = new(typeof(EntityPlayerShapeRenderer), "firstPersonMeshRef", __instance);
        Field<MultiTextureMeshRef, EntityPlayerShapeRenderer> thirdPersonMeshRef = new(typeof(EntityPlayerShapeRenderer), "thirdPersonMeshRef", __instance);
        Field<MultiTextureMeshRef, EntityShapeRenderer> meshRefOpaque = new(typeof(EntityShapeRenderer), "meshRefOpaque", __instance);

        if (firstPersonMeshRef.Value != null)
        {
            firstPersonMeshRef.Value.Dispose();
            firstPersonMeshRef.Value = null;
        }

        if (thirdPersonMeshRef.Value != null)
        {
            thirdPersonMeshRef.Value.Dispose();
            thirdPersonMeshRef.Value = null;
        }

        meshRefOpaque.Value = null;
    }

    static private void loadJointIdsRecursive(ElementPose elementPose, HashSet<int> outList)
    {
        outList.Add(elementPose.ForElement.JointId);
        foreach (ElementPose childElementPose in elementPose.ChildElementPoses)
        {
            loadJointIdsRecursive(childElementPose, outList);
        }
    }
}

internal sealed class Field<TValue, TInstance>
{
    public TValue? Value
    {
        get
        {
            return (TValue?)mFieldInfo?.GetValue(mInstance);
        }
        set
        {
            mFieldInfo?.SetValue(mInstance, value);
        }
    }

    private readonly FieldInfo? mFieldInfo;
    private readonly TInstance mInstance;

    public Field(Type from, string field, TInstance instance)
    {
        mInstance = instance;
        mFieldInfo = from.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
    }
}

internal sealed class Property<TValue, TInstance>
{
    public TValue? Value
    {
        get
        {
            return (TValue?)mPropertyInfo?.GetValue(mInstance);
        }
        set
        {
            mPropertyInfo?.SetValue(mInstance, value);
        }
    }

    private readonly PropertyInfo? mPropertyInfo;
    private readonly TInstance mInstance;

    public Property(Type from, string property, TInstance instance)
    {
        mInstance = instance;
        mPropertyInfo = from.GetProperty(property, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
    }
}