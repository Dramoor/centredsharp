﻿using CentrED.IO;
using CentrED.IO.Models;
using CentrED.Map;
using ClassicUO.Assets;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static CentrED.Application;
using Vector2 = System.Numerics.Vector2;

namespace CentrED.UI.Windows;

public class TilesWindow : Window
{
    record struct TileInfo(int RealIndex, Texture2D Texture, Rectangle Bounds, string Name);
    private static readonly Random _random = new();

    public TilesWindow()
    {
        CEDClient.Connected += FilterTiles;
    }

    public override string Name => "Tiles";
    public override WindowState DefaultState => new()
    {
        IsOpen = true
    };

    private string _filter = "";
    internal int SelectedLandId;
    internal int SelectedStaticId;
    private bool _updateScroll;
    private bool staticMode;
    private float _tableWidth;
    public const int MaxLandIndex = ArtLoader.MAX_LAND_DATA_INDEX_COUNT;
    private static readonly Vector2 TilesDimensions = new(44, 44);
    private static readonly float TotalRowHeight = TilesDimensions.Y + ImGui.GetStyle().ItemSpacing.Y;
    public const string Static_DragDrop_Target_Type = "StaticDragDrop";
    public const string Land_DragDrop_Target_Type = "LandDragDrop";

    private int[] _matchedLandIds;
    private int[] _matchedStaticIds;

    public bool LandMode => !staticMode;
    public bool StaticMode => staticMode;

    public ushort SelectedId => (ushort)(LandMode ? SelectedLandId : SelectedStaticId);

    public ushort ActiveId =>
        ActiveTileSetValues.Length > 0 ? ActiveTileSetValues[_random.Next(ActiveTileSetValues.Length)] : SelectedId;

    private void FilterTiles()
    {
        if (_filter.Length == 0)
        {
            _matchedLandIds = new int[CEDGame.MapManager.ValidLandIds.Length];
            CEDGame.MapManager.ValidLandIds.CopyTo(_matchedLandIds, 0);

            _matchedStaticIds = new int[CEDGame.MapManager.ValidStaticIds.Length];
            CEDGame.MapManager.ValidStaticIds.CopyTo(_matchedStaticIds, 0);
        }
        else
        {
            var filter = _filter.ToLower();
            var matchedLandIds = new List<int>();
            foreach (var index in CEDGame.MapManager.ValidLandIds)
            {
                var name = TileDataLoader.Instance.LandData[index].Name?.ToLower() ?? "";
                if (name.Contains(filter) || $"{index}".Contains(_filter) || $"0x{index:x4}".Contains(filter))
                    matchedLandIds.Add(index);
            }
            _matchedLandIds = matchedLandIds.ToArray();

            var matchedStaticIds = new List<int>();
            foreach (var index in CEDGame.MapManager.ValidStaticIds)
            {
                var name = TileDataLoader.Instance.StaticData[index].Name?.ToLower() ?? "";
                if (name.Contains(filter) || $"{index}".Contains(_filter) || $"0x{index:x4}".Contains(filter))
                    matchedStaticIds.Add(index);
            }
            _matchedStaticIds = matchedStaticIds.ToArray();
        }
    }

    protected override void InternalDraw()
    {
        if (ImGui.Button("Scroll to selected"))
        {
            _updateScroll = true;
        }
        ImGui.Text("Filter");
        if (ImGui.InputText("", ref _filter, 64))
        {
            FilterTiles();
        }
        if (UIManager.TwoWaySwitch("Land", "Statics", ref staticMode))
        {
            _updateScroll = true;
            _tileSetIndex = 0;
            ActiveTileSetValues = Empty;
        }
        DrawTiles();
        DrawTileSets();
    }

    private void DrawTiles()
    {
        ImGui.BeginChild("Tiles", new Vector2(), ImGuiChildFlags.Border | ImGuiChildFlags.ResizeY);
        if (ImGui.BeginTable("TilesTable", 3) && CEDClient.Initialized)
        {
            unsafe
            {
                ImGuiListClipperPtr clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("0x0000").X);
                ImGui.TableSetupColumn("Graphic", ImGuiTableColumnFlags.WidthFixed, TilesDimensions.X);
                _tableWidth = ImGui.GetContentRegionAvail().X;
                var ids = LandMode ? _matchedLandIds : _matchedStaticIds;
                clipper.Begin(ids.Length, TotalRowHeight);
                while (clipper.Step())
                {
                    for (int rowIndex = clipper.DisplayStart; rowIndex < clipper.DisplayEnd; rowIndex++)
                    {
                        int tileIndex = ids[rowIndex];
                        var tileInfo = LandMode ? LandInfo(tileIndex) : StaticInfo(ids[rowIndex]);
                        var posY = ImGui.GetCursorPosY();
                        DrawTileRow(tileIndex, tileInfo);
                        ImGui.SetCursorPosY(posY);
                        if (ImGui.Selectable
                            (
                                $"##tile{tileInfo.RealIndex}",
                                LandMode ? SelectedLandId == tileIndex : SelectedStaticId == tileIndex,
                                ImGuiSelectableFlags.SpanAllColumns,
                                new Vector2(0, TilesDimensions.Y)
                            ))
                        {
                            if (LandMode)
                                SelectedLandId = tileIndex;
                            else
                                SelectedStaticId = tileIndex;
                        }
                        if (StaticMode && ImGui.BeginPopupContextItem())
                        {
                            if (_tileSetIndex != 0 && ImGui.Button("Add to set"))
                            {
                                AddToTileSet((ushort)tileIndex);
                                ImGui.CloseCurrentPopup();
                            }
                            if (ImGui.Button("Filter"))
                            {
                                CEDGame.MapManager.StaticFilterIds.Add(tileIndex);
                                ImGui.CloseCurrentPopup();
                            }
                            ImGui.EndPopup();
                        }
                        if (ImGui.BeginDragDropSource())
                        {
                            ImGui.SetDragDropPayload
                            (
                                LandMode ? Land_DragDrop_Target_Type : Static_DragDrop_Target_Type,
                                (IntPtr)(&tileIndex),
                                sizeof(int)
                            );
                            ImGui.Text(tileInfo.Name);
                            CEDGame.UIManager.DrawImage(tileInfo.Texture, tileInfo.Bounds, TilesDimensions);
                            ImGui.EndDragDropSource();
                        }
                    }
                }
                clipper.End();
                if (_updateScroll)
                {
                    float itemPosY = clipper.StartPosY + TotalRowHeight * Array.IndexOf
                        (ids, LandMode ? SelectedLandId : SelectedStaticId);
                    ImGui.SetScrollFromPosY(itemPosY - ImGui.GetWindowPos().Y);
                    _updateScroll = false;
                }
            }
            ImGui.EndTable();
        }
        ImGui.EndChild();
    }

    private int _tileSetIndex;
    private string _tileSetName;
    private ushort _tileSetSelectedId;
    private bool _tileSetShowPopupNew;
    private bool _tileSetShowPopupDelete;
    private string _tileSetNewName = "";
    private static readonly ushort[] Empty = Array.Empty<ushort>();
    public ushort[] ActiveTileSetValues = Empty;

    private void DrawTileSets()
    {
        ImGui.BeginChild("TileSets");
        ImGui.Text("Tile Set");
        if (ImGui.Button("New"))
        {
            ImGui.OpenPopup("NewTileSet");
            _tileSetShowPopupNew = true;
        }
        ImGui.SameLine();
        ImGui.BeginDisabled(_tileSetIndex == 0);
        if (ImGui.Button("Delete"))
        {
            ImGui.OpenPopup("DeleteTileSet");
            _tileSetShowPopupDelete = true;
        }
        ImGui.EndDisabled();
        var tileSets = LandMode ?
            ProfileManager.ActiveProfile.LandTileSets :
            ProfileManager.ActiveProfile.StaticTileSets;
        //Probably slow, optimize
        var names = new[] { String.Empty }.Concat(tileSets.Keys).ToArray();
        if (ImGui.Combo("", ref _tileSetIndex, names, names.Length))
        {
            _tileSetName = names[_tileSetIndex];
            if (_tileSetIndex == 0)
            {
                ActiveTileSetValues = Empty;
            }
            else
            {
                ActiveTileSetValues = tileSets[_tileSetName].ToArray();
            }
        }
        ImGui.BeginChild("TileSetTable");
        if (ImGui.BeginTable("TileSetTable", 3) && CEDClient.Initialized)
        {
            unsafe
            {
                ImGuiListClipperPtr clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("0x0000").X);
                ImGui.TableSetupColumn("Graphic", ImGuiTableColumnFlags.WidthFixed, TilesDimensions.X);
                _tableWidth = ImGui.GetContentRegionAvail().X;
                var ids = ActiveTileSetValues; //We copy the array here to not crash when removing, please fix :)
                clipper.Begin(ids.Length, TotalRowHeight);
                while (clipper.Step())
                {
                    for (int rowIndex = clipper.DisplayStart; rowIndex < clipper.DisplayEnd; rowIndex++)
                    {
                        var tileIndex = ids[rowIndex];
                        var tileInfo = LandMode ? LandInfo(tileIndex) : StaticInfo(tileIndex);
                        var posY = ImGui.GetCursorPosY();
                        DrawTileRow(tileIndex, tileInfo);
                        ImGui.SetCursorPosY(posY);
                        if (ImGui.Selectable
                            (
                                $"##tileset{tileInfo.RealIndex}",
                               _tileSetSelectedId == tileIndex,
                                ImGuiSelectableFlags.SpanAllColumns,
                                new Vector2(0, TilesDimensions.Y)
                            ))
                        {
                            _tileSetSelectedId = tileIndex;
                        }
                        if (StaticMode && ImGui.BeginPopupContextItem())
                        {
                            if (ImGui.Button("Remove"))
                            {
                                RemoveFromTileSet(tileIndex);
                                ImGui.CloseCurrentPopup();
                            }
                            ImGui.EndPopup();
                        }
                    }
                }
                clipper.End();
            }
            ImGui.EndTable();
        }
        ImGui.EndChild();
        if (_tileSetIndex != 0 && ImGui.BeginDragDropTarget())
        {
            var payloadPtr = ImGui.AcceptDragDropPayload
                (LandMode ? Land_DragDrop_Target_Type : Static_DragDrop_Target_Type);
            unsafe
            {
                if (payloadPtr.NativePtr != null)
                {
                    var dataPtr = (int*)payloadPtr.Data;
                    int id = dataPtr[0];
                    AddToTileSet((ushort)id);
                }
            }
            ImGui.EndDragDropTarget();
        }
        if (ImGui.BeginPopupModal
            (
                "NewTileSet",
                ref _tileSetShowPopupNew,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar
            ))
        {
            ImGui.Text("Name");
            ImGui.SameLine();
            ImGui.InputText("", ref _tileSetNewName, 32);
            if (ImGui.Button("Add"))
            {
                tileSets.Add(_tileSetNewName, new SortedSet<ushort>());
                _tileSetIndex = Array.IndexOf(tileSets.Keys.ToArray(), _tileSetNewName) + 1;
                _tileSetName = _tileSetNewName;
                ActiveTileSetValues = Empty;
                ProfileManager.Save();
                _tileSetNewName = "";
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
        if (ImGui.BeginPopupModal
            (
                "DeleteTileSet",
                ref _tileSetShowPopupDelete,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar
            ))
        {
            ImGui.Text($"Are you sure you want to delete tile set '{_tileSetName}'?");
            if (ImGui.Button("Yes"))
            {
                tileSets.Remove(_tileSetName);
                ProfileManager.Save();
                _tileSetIndex--;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("No"))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
        ImGui.EndChild();
    }

    private void AddToTileSet(ushort id)
    {
        var tileSets = LandMode ?
            ProfileManager.ActiveProfile.LandTileSets :
            ProfileManager.ActiveProfile.StaticTileSets;
        var tileSet = tileSets[_tileSetName];
        tileSet.Add(id);
        ActiveTileSetValues = tileSet.ToArray();
        ProfileManager.Save();
    }

    private void RemoveFromTileSet(ushort id)
    {
        var tileSets = LandMode ?
            ProfileManager.ActiveProfile.LandTileSets :
            ProfileManager.ActiveProfile.StaticTileSets;
        var tileSet = tileSets[_tileSetName];
        tileSet.Remove(id);
        ActiveTileSetValues = tileSet.ToArray();
        ProfileManager.Save();
    }

    private TileInfo LandInfo(int index)
    {
        var texture = ArtLoader.Instance.GetLandTexture((uint)index, out var bounds);
        var name = TileDataLoader.Instance.LandData[index].Name;
        return new(index, texture, bounds, name);
    }

    private TileInfo StaticInfo(int index)
    {
        var realIndex = index + MaxLandIndex;
        var texture = ArtLoader.Instance.GetStaticTexture((uint)index, out var bounds);
        var realBounds = ArtLoader.Instance.GetRealArtBounds(index);
        var name = TileDataLoader.Instance.StaticData[index].Name;
        return new
        (
            realIndex,
            texture,
            new Rectangle(bounds.X + realBounds.X, bounds.Y + realBounds.Y, realBounds.Width, realBounds.Height),
            name
        );
    }

    private void DrawTileRow(int index, TileInfo tileInfo)
    {
        ImGui.TableNextRow(ImGuiTableRowFlags.None, TilesDimensions.Y);
        if (ImGui.TableNextColumn())
        {
            ImGui.SetCursorPosY
                (ImGui.GetCursorPosY() + (TilesDimensions.Y - ImGui.GetFontSize()) / 2); //center vertically
            ImGui.Text($"0x{index:X4}");
        }

        if (ImGui.TableNextColumn())
        {
            CEDGame.UIManager.DrawImage(tileInfo.Texture, tileInfo.Bounds, TilesDimensions);
        }

        if (ImGui.TableNextColumn())
        {
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (TilesDimensions.Y - ImGui.GetFontSize()) / 2);
            ImGui.TextUnformatted(tileInfo.Name);
        }
    }

    public void UpdateSelectedId(TileObject mapObject)
    {
        if (mapObject is StaticObject)
            SelectedStaticId = mapObject.Tile.Id;
        else if (mapObject is LandObject)
            SelectedLandId = mapObject.Tile.Id;
        _updateScroll = true;
    }
}