namespace coil{
    public class Seg{
        public (int,int) Start {get;private set;}
        public Dir Dir {get; private set;}
        
        //a seg covering 2 squars has a length of 1.  squares "covered" is len-1
        public int Len {get;set;}
        public int Index {get; set;} = 0;
        public Seg((int,int) start, Dir dir, int len){
            Start=start;
            Dir = dir;
            Len = len;
        }

        public override string ToString(){
            return $"Seg{Index,5}: {Start} going {Dir,4} ({Len})";
        }
    }
}