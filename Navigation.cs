using System;

namespace coil{
    public static class Navigation{
        public static (int,int) Add((int,int) start, Dir dir){
            var x = start.Item1;
            var y = start.Item2;
            switch (dir){
                case Dir.Up:
                    return (x,y-1);
                case Dir.Right:
                    return (x+1,y);
                case Dir.Down:
                    return (x,y+1);
                case Dir.Left:
                    return (x-1,y);
                default:
                    throw new Exception("x");
            }
        }

        public static Dir Rot(Dir dir){
            switch (dir){
                case Dir.Up:
                    return Dir.Right;
                case Dir.Right:
                    return Dir.Down;
                case Dir.Down:
                    return Dir.Left;
                case Dir.Left:
                    return Dir.Up;
                default:
                    throw new Exception("x");
            }
        }

        public static Dir ARot(Dir dir){
            switch (dir){
                case Dir.Up:
                    return Dir.Left;
                case Dir.Right:
                    return Dir.Up;
                case Dir.Down:
                    return Dir.Right;
                case Dir.Left:
                    return Dir.Down;
                default:
                    throw new Exception("x");
            }
        }

        public static (int,int) Add((int,int) start, Dir dir, int n){
            if (n<1){
                throw new Exception("Invalid n");
            }
            var x = start.Item1;
            var y = start.Item2;
            switch (dir){
                case Dir.Up:
                    return (x,y-n);
                case Dir.Right:
                    return (x+n,y);
                case Dir.Down:
                    return (x,y+n);
                case Dir.Left:
                    return (x-n,y);
                default:
                    throw new Exception("x");
            }
        }

        public static (int,int) GetEnd(Seg seg){
            var x = seg.Start.Item1;
            var y = seg.Start.Item2;
            switch (seg.Dir){
                case Dir.Up:
                    return (x, y-seg.Len);
                case Dir.Right:
                    return (x+seg.Len, y);
                case Dir.Down:
                    return (x, y+seg.Len);
                case Dir.Left:
                    return (x-seg.Len, y);
                default:
                    throw new Exception("Bad seg dir.");
            }
        }
    }
}