namespace Consensus.Backend.DTOs.Outgoing
{
    public class HivesPagedSet
    {
        public int TotalPages { get; set; }
        public HiveManifestDto[] Hives { get; set; }
    }
}