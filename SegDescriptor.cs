namespace coil
{
    /// <summary>
    /// For constructing test cases
    /// </summary>
    public class SegDescriptor
    {
        public Dir Dir;
        
        public int Len;
        
        public SegDescriptor(Dir d, int l)
        {
            Dir = d;
            Len = l;
        }
        public static SegDescriptor GetSD(Dir d, int l)
        {
            return new SegDescriptor(d, l);
        }
    }
}
