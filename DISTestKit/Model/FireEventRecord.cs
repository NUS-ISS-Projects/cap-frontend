using System;

namespace DISTestKit.Model
{
public class FireEventRecord
    {
        public int Id { get; set; }
        public int FiringSite { get; set; }
        public int FiringApplication { get; set; }
        public int FiringEntity { get; set; }
        public int TargetSite { get; set; }
        public int TargetApplication { get; set; }
        public int TargetEntity { get; set; }
        public int MunitionSite { get; set; }
        public int MunitionApplication { get; set; }
        public int MunitionEntity { get; set; }
        public long Timestamp { get; set; }
    }
}