using System;
using System.Collections.Generic;
using UnityEngine;

namespace PWTA
{

    public class FoliageGrid
    {
        public float GridSize = 10f;
        public int LoadRadius = 2;
        private HashSet<Vector2Int> _activeCoord = new HashSet<Vector2Int>();
        
        public event Action<HashSet<Vector2Int>> OnActiveGridChanged;
        private Vector2Int lastCoord;
        private Vector3 lastPosition;
        private Vector2Int _gridMinCoord;
        private Vector2Int _gridMaxCoord;

        public void ApplyCoord(IEnumerable<IFoliageElement> foliageCompactData)
        {            
            foreach(var data in foliageCompactData)
            {
                var gridCoord = GetCoord(data.Position);
                
                if(_gridMinCoord.x > gridCoord.x)
                    _gridMinCoord.x = gridCoord.x;
                if(_gridMinCoord.y > gridCoord.y)
                    _gridMinCoord.y = gridCoord.y;
                if(_gridMaxCoord.x < gridCoord.x)
                    _gridMaxCoord.x = gridCoord.x;
                if(_gridMaxCoord.y < gridCoord.y)
                    _gridMaxCoord.y = gridCoord.y;
            }
            lastPosition = Vector3.one * float.MinValue;
            lastCoord = Vector2Int.one * int.MinValue;
        }

        public int RowCount => _gridMaxCoord.y - _gridMinCoord.y + 1;
        public int ColumnCount => _gridMaxCoord.x - _gridMinCoord.x + 1;


        public bool IsDebug = true;
        public bool IsActiveCoord(Vector2Int coord)
        {
            if(IsDebug ||_activeCoord.Contains(coord))
                return true;

            return false;
        }

        public IEnumerable<Vector2Int> GetActiveCoords()
        {
            return _activeCoord;
        }

        public void Update(Vector3 currentPosition)
        {
            if(lastPosition == currentPosition)
                return;

            Vector2Int centerCoord = GetCoord(currentPosition);
            if(lastCoord != centerCoord)
            {
                lastCoord = centerCoord;
                UpdateActiveGrid(centerCoord);
            }

            lastPosition = currentPosition;
        }

        private void UpdateActiveGrid(Vector2Int currentCoord)
        {           
            HashSet<Vector2Int> targetCoords = new HashSet<Vector2Int>();
            for (int x = -LoadRadius; x <= LoadRadius; x++)
            {
                for (int y = -LoadRadius; y <= LoadRadius; y++)
                {
                    Vector2Int coord = currentCoord + new Vector2Int(x, y);
                    targetCoords.Add(coord);
                }
            }

            _activeCoord = targetCoords;
            OnActiveGridChanged?.Invoke(targetCoords);
        }

        public Vector2Int GetCoord(Vector3 position)
        {
            int x = Mathf.FloorToInt(position.x / GridSize);
            int y = Mathf.FloorToInt(position.z / GridSize);
            return new Vector2Int(x, y);
        }
    }
}