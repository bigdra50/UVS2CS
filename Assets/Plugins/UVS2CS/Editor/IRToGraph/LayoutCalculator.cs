using UnityEngine;

namespace UVS2CS.IRToGraph
{
    public sealed class LayoutCalculator
    {
        const float NodeWidth = 250f;
        const float NodeHeight = 80f;
        const float HorizontalGap = 100f;
        const float VerticalGap = 40f;

        int _currentX;
        int _currentY;

        public Vector2 Next()
        {
            var pos = new Vector2(_currentX * (NodeWidth + HorizontalGap),
                                  _currentY * (NodeHeight + VerticalGap));
            _currentX++;
            return pos;
        }

        public void NewRow()
        {
            _currentX = 1;
            _currentY++;
        }

        public Vector2 EventPosition()
        {
            var pos = new Vector2(0, _currentY * (NodeHeight + VerticalGap + 60f));
            _currentX = 1;
            return pos;
        }

        public void NextMethod()
        {
            _currentX = 0;
            _currentY += 3;
        }
    }
}
