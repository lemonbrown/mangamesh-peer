namespace MangaMesh.Peer.Core.Replication;

public interface IChapterDiversityTracker
{
    /// <summary>
    /// Returns true if the local peer may accept one more chunk from this chapter
    /// without exceeding the configured diversity ratio.
    /// </summary>
    bool CanAcceptChunk(string chapterId, int totalChunksInChapter);

    /// <summary>Records that the local peer has accepted one more chunk from this chapter.</summary>
    void RecordChunkAccepted(string chapterId);

    /// <summary>Returns how many chunks of this chapter the local peer currently holds.</summary>
    int GetLocalChunkCount(string chapterId);
}
