using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Connects the runtime touch INV/hotbar buttons to TABZ's existing UIInventory.
/// It intentionally uses reflection because the original methods/field are
/// private in the recovered game assembly. This keeps the adapter optional and
/// avoids maintaining a second inventory implementation.
/// </summary>
public class MobileInventoryBridge : MonoBehaviour
{
    public MonoBehaviour inventoryComponent;
    public bool logMissingInventory = true;

    private MethodInfo toggleOpen;
    private MethodInfo updateHotbarSelection;
    private FieldInfo selectedHotbarIndex;
    private bool warned;

    private void Awake()
    {
        ResolveInventory();
    }

    private void LateUpdate()
    {
        // UI EventSystem pointer callbacks normally run during Update; consume
        // the one-shot state here so buttons remain responsive without a
        // dependency on script execution order in Update.
        if (MobileInputState.InventoryTogglePressed)
        {
            MobileInputState.InventoryTogglePressed = false;
            if (ResolveInventory())
                toggleOpen.Invoke(inventoryComponent, null);
        }

        int slot = MobileInputState.HotbarSlotPressed;
        if (slot >= 0)
        {
            MobileInputState.HotbarSlotPressed = -1;
            if (ResolveInventory() && selectedHotbarIndex != null && updateHotbarSelection != null)
            {
                selectedHotbarIndex.SetValue(inventoryComponent, slot);
                updateHotbarSelection.Invoke(inventoryComponent, null);
            }
        }
    }

    private bool ResolveInventory()
    {
        if (inventoryComponent != null && toggleOpen != null && updateHotbarSelection != null)
            return true;

        if (inventoryComponent == null)
        {
            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour candidate = behaviours[i];
                if (candidate != null && candidate.GetType().Name == "UIInventory")
                {
                    inventoryComponent = candidate;
                    break;
                }
            }
        }

        if (inventoryComponent == null)
        {
            if (logMissingInventory && !warned)
            {
                Debug.LogWarning("MobileInventoryBridge: original UIInventory was not found. Inventory buttons will stay inactive until the TABZ inventory component is present.");
                warned = true;
            }
            return false;
        }

        Type type = inventoryComponent.GetType();
        toggleOpen = type.GetMethod("ToggleOpen", BindingFlags.Instance | BindingFlags.NonPublic);
        updateHotbarSelection = type.GetMethod("UpdateHotbarSelection", BindingFlags.Instance | BindingFlags.NonPublic);
        selectedHotbarIndex = type.GetField("m_selectedHotbarIndex", BindingFlags.Instance | BindingFlags.NonPublic);

        if (toggleOpen == null || updateHotbarSelection == null || selectedHotbarIndex == null)
        {
            if (logMissingInventory && !warned)
            {
                Debug.LogWarning("MobileInventoryBridge: UIInventory internals differ from the recovered v5.1 baseline; check ToggleOpen, UpdateHotbarSelection and m_selectedHotbarIndex.");
                warned = true;
            }
            return false;
        }

        return true;
    }
}
