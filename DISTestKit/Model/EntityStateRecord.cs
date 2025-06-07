using System;

namespace DISTestKit.Model
{
public class EntityStateRecord
    {
        public int Id { get; set; }
        public int Site { get; set; }
        public int Application { get; set; }
        public int Entity { get; set; }
        public double LocationX { get; set; }
        public double LocationY { get; set; }
        public double LocationZ { get; set; }
        public long Timestamp { get; set; }
    }
}