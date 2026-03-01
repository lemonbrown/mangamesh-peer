using System.ComponentModel.DataAnnotations;

namespace MangaMesh.Shared.Data.Entities
{
    public class ChapterFlagEntity
    {
        [Key]
        public string Id { get; set; } = default!;

        public string ManifestHash { get; set; } = default!;
        public string SeriesId { get; set; } = default!;
        public string ChapterId { get; set; } = default!;

        /// <summary>JSON-encoded string array of flag category IDs (e.g. ["quality_low","nsfw"]).</summary>
        public string Categories { get; set; } = default!;

        public string? Comment { get; set; }
        public DateTime SubmittedUtc { get; set; }
        public bool Dismissed { get; set; }
        /// <summary>Optional: the NodeId of the peer reported as delivering bad content.</summary>
        public string? ReportedNodeId { get; set; }
    }
}
