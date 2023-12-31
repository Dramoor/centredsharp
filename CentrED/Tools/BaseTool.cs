﻿using CentrED.Map;
using Microsoft.Xna.Framework.Input;
using static CentrED.Application;

namespace CentrED.Tools;

//BaseTool allows for out of the box continous and area drawing
public abstract class BaseTool : Tool
{
    protected abstract void GhostApply(TileObject? o);
    protected abstract void GhostClear(TileObject? o);
    protected abstract void Apply(TileObject? o);
    
    protected bool _pressed;
    protected bool _areaMode;
    private TileObject? _areaStartTile;
    
    public sealed override void OnKeyPressed(Keys key)
    {
        if (key == Keys.LeftControl && !_pressed)
        {
            _areaMode = true;
        }
    }
    
    public sealed override void OnKeyReleased(Keys key)
    {
        if (key == Keys.LeftControl && !_pressed)
        {
            _areaMode = false;
        }
    }
    
    public sealed override void OnMousePressed(TileObject? o)
    {
        _pressed = true;
        if (_areaMode && _areaStartTile == null && o != null)
        {
            _areaStartTile = o;
        }
    }
    
    public sealed override void OnMouseReleased(TileObject? o)
    {
        if(_pressed)
        {
            if (_areaMode)
            {
                foreach (var to in CEDGame.MapManager.GetTopTiles(_areaStartTile, o))
                {
                    Apply(to);   
                    GhostClear(to);

                }
            }
            else
            {
                Apply(o);
                GhostClear(o);
            }
        }
        
        _pressed = false;
        _areaStartTile = null;
    }

    
    public sealed override void OnMouseEnter(TileObject? o)
    {
        if (o == null)
            return;

        if (_areaMode && _pressed)
        {
            foreach (var to in CEDGame.MapManager.GetTopTiles(_areaStartTile, o))
            {
                GhostApply(to);   
            }
        }
        else
        {
            GhostApply(o);
        }
    }
    
    public sealed override void OnMouseLeave(TileObject? o)
    {
        if (_pressed && !_areaMode)
        {
            Apply(o);
        }
        if (_pressed && _areaMode)
        {
            foreach (var to in CEDGame.MapManager.GetTopTiles(_areaStartTile, o))
            {
                GhostClear(to);
            }
        }
        else
        {
            GhostClear(o);
        }
    }
}