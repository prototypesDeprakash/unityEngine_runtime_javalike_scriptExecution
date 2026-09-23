using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central farming system. One PlantDefinition per crop (grow time, how much
/// one harvest gives, and its prefab) is set in the Inspector. This manager
/// also tracks what is currently planted on every Farmland cell, of every
/// grid, so there is a single source of truth - agents never store plant
/// state themselves.
/// Only Drone exposes Plant/CanHarvest/Harvest; Player can't farm (see
/// ProgrammableAgent - the base versions are "unsupported").
/// </summary>
[Serializable]
public class PlantDefinition
{
    public ItemType crop;

    [Tooltip("Seconds from planting until it can be harvested.")]
    public float growDuration = 20f;

    [Tooltip("How many items ONE harvest() gives.")]
    public int harvestYield = 3;

    [Tooltip("Spawned on the cell while this crop is planted.")]
    public GameObject prefab;
}

public class PlantsManager : MonoBehaviour
{
    [SerializeField] private List<PlantDefinition> plants = new List<PlantDefinition>();

    // One entry per Farmland cell that currently has something growing.
    private class Planted
    {
        public ItemType crop;
        public float readyAt;
        public GameObject visual;
    }

    // Key = (grid, cell). A struct key so two different grids never collide.
    private readonly Dictionary<(WorldGrid, Vector2Int), Planted> planted =
        new Dictionary<(WorldGrid, Vector2Int), Planted>();

    public bool TryGetDefinition(ItemType crop, out PlantDefinition def)
    {
        foreach (PlantDefinition p in plants)
        {
            if (p.crop == crop)
            {
                def = p;
                return true;
            }
        }

        def = null;
        return false;
    }

    public bool IsPlantable(ItemType crop) => TryGetDefinition(crop, out _);

    // --------------------------------------------------
    // PLANTING - replaces whatever was already on the cell, if anything.
    // Returns false only if 'crop' has no PlantDefinition.
    // --------------------------------------------------

    public bool Plant(WorldGrid grid, Vector2Int cell, ItemType crop, string who, out bool replaced)
    {
        replaced = false;

        if (!TryGetDefinition(crop, out PlantDefinition def))
        {
            GameLog.Warn($"{who}: {crop.DisplayName()} can't be planted (no PlantDefinition for it).");
            return false;
        }

        var key = (grid, cell);

        if (planted.TryGetValue(key, out Planted existing))
        {
            replaced = true;
            if (existing.visual != null)
                Destroy(existing.visual);
            planted.Remove(key);
        }

        GameObject visual = null;
        if (def.prefab != null)
            visual = Instantiate(def.prefab, grid.GridCellCenter(cell.x, cell.y), Quaternion.identity, grid.transform);

        planted[key] = new Planted
        {
            crop = crop,
            readyAt = Time.time + Mathf.Max(0f, def.growDuration),
            visual = visual,
        };

        GameLog.Info(replaced
            ? $"{who}: replaced the crop here with {crop.DisplayName()}."
            : $"{who}: planted {crop.DisplayName()}.");

        return true;
    }

    // --------------------------------------------------
    // READING
    // --------------------------------------------------

    public bool HasPlant(WorldGrid grid, Vector2Int cell) => planted.ContainsKey((grid, cell));

    public bool TryGetPlant(WorldGrid grid, Vector2Int cell, out ItemType crop, out bool ready)
    {
        crop = ItemType.None;
        ready = false;

        if (!planted.TryGetValue((grid, cell), out Planted p))
            return false;

        crop = p.crop;
        ready = Time.time >= p.readyAt;
        return true;
    }

    public bool CanHarvest(WorldGrid grid, Vector2Int cell)
    {
        return TryGetPlant(grid, cell, out _, out bool ready) && ready;
    }

    // --------------------------------------------------
    // HARVESTING - clears the cell and reports how much of what grew.
    // --------------------------------------------------

    public bool TryHarvest(WorldGrid grid, Vector2Int cell, string who, out ItemType crop, out int amount)
    {
        crop = ItemType.None;
        amount = 0;

        var key = (grid, cell);

        if (!planted.TryGetValue(key, out Planted p))
        {
            GameLog.Warn($"{who}: nothing planted here.");
            return false;
        }

        if (Time.time < p.readyAt)
        {
            GameLog.Warn($"{who}: {p.crop.DisplayName()} isn't ready yet ({(p.readyAt - Time.time):0.0}s left).");
            return false;
        }

        TryGetDefinition(p.crop, out PlantDefinition def);

        crop = p.crop;
        amount = def != null ? def.harvestYield : 1;

        if (p.visual != null)
            Destroy(p.visual);

        planted.Remove(key);

        return true;
    }
}
