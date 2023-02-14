using Assets.Scripts;
using Assets.Scripts.Inventory;
using Assets.Scripts.Objects;
using Assets.Scripts.UI;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using System.Collections.Generic;
using static Assets.Scripts.Inventory.InventoryManager;
using Assets.Scripts.Serialization;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.Util;

namespace StationeersZoop
{
    [HarmonyPatch(typeof(InventoryManager), "SetMultiConstructorItemPlacement")]
    public class InventoryManagerSetMultiContstruct
    {
        [UsedImplicitly]
        public static void Prefix(InventoryManager __instance, MultiConstructor multiConstructorItem)
        {
            ConsoleWindow.Print("detected: " + multiConstructorItem.PrefabHash);
            ZoopUtility.StartZoop();
        }
    }
    [HarmonyPatch(typeof(InventoryManager), "SetConstructorItemPlacement")]
    public class InventoryManagerSetContstruct
    {
        [UsedImplicitly]
        public static void Prefix(InventoryManager __instance, Constructor constructorItem)
        {
            ConsoleWindow.Print("detected: " + constructorItem.PrefabHash);
            ZoopUtility.StartZoop();
        }
    }
    [HarmonyPatch(typeof(InventoryManager), "CancelPlacement")]
    public class InventoryManagerCancelPlacement
    {
        [UsedImplicitly]
        public static void Prefix(InventoryManager __instance)
        {
            ZoopUtility.CancelZoop();
            ZoopUtility.isZoopMode = false;
        }
    }
    [HarmonyPatch(typeof(InventoryManager), "UsePrimaryComplete")]
    public class InventoryManagerUsePrimaryComplete
    {
        [UsedImplicitly]
        public static void Prefix(InventoryManager __instance)
        {
            ZoopUtility.BuildZoop(__instance);
            ZoopUtility.isZoopMode = false;
        }
    }
    [HarmonyPatch(typeof(InventoryManager), "PlacementMode")]
    public class InventoryManagerPlacementMode
    {
        [UsedImplicitly]
        public static bool Prefix(InventoryManager __instance)
        {
            bool scrollUp = __instance.newScrollData > 0f;
            bool scrollDown = __instance.newScrollData < 0f;
            ZoopUtility.isZoopMode = KeyManager.GetButton(ZoopUtility.ZoopMode);
            bool isSpaceMode = KeyManager.GetButton(ZoopUtility.SpaceMode);
            bool secondary = KeyManager.GetMouseDown("Secondary");
            bool primary = KeyManager.GetMouseDown("Primary");
            if (ZoopUtility.isZoopMode && isSpaceMode && scrollUp)
            {
                ZoopUtility.spacing += 1;
            }
            if (ZoopUtility.isZoopMode && isSpaceMode && scrollDown)
            {
                ZoopUtility.spacing -= 1;
            }
            if (ZoopUtility.isZoopMode && !isSpaceMode && scrollUp)
            {
                ZoopUtility.AddStructure();
            }
            if (ZoopUtility.isZoopMode && !isSpaceMode && scrollDown)
            {
                ZoopUtility.ReduceStructure();
            }
            if (!ZoopUtility.isZoopMode && secondary)
            {
                ZoopUtility.CancelZoop();
            }
            if (ZoopUtility.isZoopMode && !isSpaceMode && secondary)
            {
                ZoopUtility.CycleAxis();
            }
            if (ZoopUtility.isZoopMode && !isSpaceMode && primary)
            {
                ZoopUtility.stickGhost = !ZoopUtility.stickGhost;
            }
            return !(ZoopUtility.isZoopMode);
        }

        [UsedImplicitly]
        public static void Postfix(InventoryManager __instance, PassiveTooltip ___tooltip)
        {
            if (InventoryManager.CurrentMode == Mode.Placement)
            {
                ___tooltip.Title += " +" + ZoopUtility.structures.Count;
                ZoopUtility.UpdateZoop(__instance);
            }
        }
    }
    //[HarmonyPatch(typeof(InventoryManager), "UpdatePlacement", new Type[] { typeof(Structure) })]
    //public class InventoryManagerUpdatePlacement
    //{
    //    [UsedImplicitly]
    //    public static void Prefix(InventoryManager __instance, Structure structure)
    //    {
    //    }
    //}

    [HarmonyPatch(typeof(ConstructionPanel), "SelectUp")]
    public class ConstructionPanelSelectUp
    {
        [UsedImplicitly]
        public static bool Prefix()
        {
            return !(ZoopUtility.isZoopMode);
        }
    }
    [HarmonyPatch(typeof(ConstructionPanel), "SelectDown")]
    public class ConstructionPanelSelectDown
    {
        [UsedImplicitly]
        public static bool Prefix()
        {
            return !(ZoopUtility.isZoopMode);
        }
    }

    public class ZoopUtility
    {
        public static KeyCode ZoopMode = KeyCode.LeftShift;
        public static KeyCode SpaceMode = KeyCode.LeftControl;
        public static List<Structure> structures;
        private static ZoopDirection zoopDirection;
        public static bool isZoopMode;
        private static bool increasing;
        public static bool stickGhost;
        public static int spacing;
        public static int count;
        public static int lastIndex;
        public static void StartZoop()
        {
            if (structures == null)
            {
                structures = new List<Structure>();
            }
            CancelZoop();
        }
        public static void CancelZoop()
        {
            while (structures.Count > 0)
            {
                ReduceStructure();
            }
            stickGhost = false;
        }
        public static void RestartZoop()
        {
            for (int i = 0; i < count; i++)
            {
                AddStructure();
            }
        }
        public static void BuildZoop(InventoryManager IM)
        {
            foreach (Structure item in structures)
            {
                if (InventoryManager.ActiveHandSlot.Occupant == null)
                {
                    break;
                }
                if (!CanConstruct(IM, item))
                {
                    continue;
                }
                if (InventoryManager.ActiveHandSlot.Occupant is MultiConstructor multiConstructor)
                {
                    int optionIndex = IM.ConstructionPanel.BuildIndex;
                    int entryQuantity = multiConstructor.Constructables[optionIndex].BuildStates[0].Tool.EntryQuantity;
                    if (multiConstructor.Quantity < entryQuantity)
                    {
                        break;
                    }
                    multiConstructor.OnUseItem(entryQuantity, null);
                    OnServer.UseMultiConstructor(InventoryManager.Parent, IM.ActiveHand.SlotId, IM.InactiveHand.SlotId, item.transform.position, item.transform.rotation, optionIndex, true/*InventoryManager.IsAuthoringMode*/, InventoryManager.ParentBrain.ClientId, IM.SpawnPrefabIndex);
                }
                else if (InventoryManager.ActiveHandSlot.Occupant is AuthoringTool)
                {
                    OnServer.UseMultiConstructor(InventoryManager.Parent, IM.ActiveHand.SlotId, IM.InactiveHand.SlotId, item.transform.position, item.transform.rotation, IM.ConstructionPanel.BuildIndex, true/*InventoryManager.IsAuthoringMode*/, InventoryManager.ParentBrain.ClientId, IM.SpawnPrefabIndex);
                }
                else
                {
                    OnServer.UseItemPrimary(InventoryManager.Parent, IM.ActiveHand.SlotId, item.transform.position, item.transform.rotation, InventoryManager.ParentBrain.ClientId, IM.SpawnPrefabIndex);
                }
            }
            count = structures.Count;
            CancelZoop();
        }
        private static bool CanConstruct(InventoryManager IM, Structure structure)
        {
            //return true;
            return structure.CanConstruct();
            //bool canConstruct = false;
            //switch (structure.PlacementType)
            //{
            //    case PlacementSnap.FaceMount:
            //        canConstruct = structure.GridController.GetStructure(structure.Position) == null;
            //        break;
            //    case PlacementSnap.Grid:
            //    case PlacementSnap.Face:
            //    default:
            //        canConstruct = structure.CanConstruct();
            //        break;
            //}
            //return canConstruct;
        }
        public static void UpdateZoop(InventoryManager IM)
        {
            if (IM.ConstructionPanel.BuildIndex != lastIndex)
            {
                lastIndex = IM.ConstructionPanel.BuildIndex;
                count = structures.Count;
                CancelZoop();
                RestartZoop();
            }
            Vector3 Location = InventoryManager.ConstructionCursor.Position;
            for (int i = 0; i < structures.Count; i++)
            {
                spacing = Mathf.Max(spacing, 1);
                float minValue = InventoryManager.ConstructionCursor is SmallGrid ? 0.5f : 2f;
                float value = increasing ? minValue * spacing : -(minValue * spacing);
                Vector3 offsett;
                switch (zoopDirection)
                {
                    case ZoopDirection.x:
                        offsett = new Vector3((i + 1) * value, 0, 0);
                        break;
                    case ZoopDirection.y:
                        offsett = new Vector3(0, (i + 1) * value, 0);
                        break;
                    case ZoopDirection.z:
                    default:
                        offsett = new Vector3(0, 0, (i + 1) * value);
                        break;
                }
                structures[i].ThingTransformPosition = (Location + offsett);
                structures[i].ThingTransformRotation = InventoryManager.ConstructionCursor.Rotation;
                structures[i].GridController = InventoryManager.ConstructionCursor.GridController;
                //structures[i].ThingTransform.forward = InventoryManager.ConstructionCursor.transform.forward; //done for testing
                //structures[i].GridSize = InventoryManager.ConstructionCursor.GridSize; //done for testing
                //structures[i].StructureCollisionType = InventoryManager.ConstructionCursor.StructureCollisionType; //done for testing
                //structures[i].SnapToLocalGrid(); //done for testing
                //structures[i].LocalGrid = structures[i].ThingTransformPosition.ToGridPosition(); //done for testing
                //structures[i].GetLocalGrid(Location + offsett); //done for testing
                //structures[i].transform.SetPositionAndRotation(Location + offsett, InventoryManager.ConstructionCursor.Rotation); //done for testing
                if ((bool)InventoryManager.ConstructionCursor.Wireframe)
                {
                    SetColor(IM, structures[i]);
                }
            }

        }
        private static void SetColor(InventoryManager IM, Structure structure)
        {
            bool canConstruct = CanConstruct(IM, structure);
            Color color = canConstruct ? Color.green : Color.red;
            if (structure is SmallGrid smallGrid)
            {
                List<Connection> list = smallGrid.WillJoinNetwork();
                foreach (Connection openEnd in smallGrid.OpenEnds)
                {
                    if (canConstruct)
                    {
                        openEnd.HelperRenderer.material.color = (list.Contains(openEnd) ? Color.yellow.SetAlpha(IM.CursorAlphaConstructionHelper) : Color.green.SetAlpha(IM.CursorAlphaConstructionHelper));
                    }
                    else
                    {
                        openEnd.HelperRenderer.material.color = Color.red.SetAlpha(IM.CursorAlphaConstructionHelper);
                    }
                }
                color = ((canConstruct && list.Count > 0) ? Color.yellow : color);
            }
            color.a = IM.CursorAlphaConstructionMesh;
            structure.Wireframe.BlueprintRenderer.material.color = color;
        }
        public static void CycleAxis()
        {
            ConsoleWindow.Print("RotationChange: " + zoopDirection);
            switch (zoopDirection)
            {
                case ZoopDirection.x:
                    zoopDirection = ZoopDirection.y;
                    break;
                case ZoopDirection.y:
                    zoopDirection = ZoopDirection.z;
                    break;
                case ZoopDirection.z:
                    zoopDirection = ZoopDirection.x;
                    increasing = !increasing;
                    break;
                default:
                    break;
            }
        }
        public static void ReduceStructure()
        {
            if (structures.Count > 0)
            {
                structures[structures.Count - 1].gameObject.SetActive(false);
                MonoBehaviour.Destroy(structures[structures.Count - 1]);
                structures.RemoveAt(structures.Count - 1);
            }
        }
        public static void AddStructure()
        {
            AddStructure(InventoryManager.ConstructionCursor);
        }
        public static void AddStructure(Structure structure)
        {
            if (InventoryManager.ActiveHandSlot.Occupant is Stackable Constructor)
            {
                if (Constructor.Quantity-1 > structures.Count)
                {
                    MakeItem(structure);
                }
            } else if (InventoryManager.ActiveHandSlot.Occupant is AuthoringTool)
            {
                MakeItem(structure);
            }
            ConsoleWindow.Print("Added new, total: " + structures.Count);
        }

        private static void MakeItem(Structure structure)
        {
            if (structure == null)
            {
                return;
            }
            Structure structureNew = MonoBehaviour.Instantiate(structure);
            if (structureNew != null)
            {
                structureNew.gameObject.SetActive(true);
                structures.Add(structureNew);
            }
            structureNew.ThingTransform.forward = structure.ThingTransform.forward; //done for testing
            structureNew.transform.rotation = structure.ThingTransform.rotation; //done for testing
        }

        public enum ZoopDirection { x, y, z};
    }
}