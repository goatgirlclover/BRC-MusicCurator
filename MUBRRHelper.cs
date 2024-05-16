using Reptile;
namespace MusicCurator
{
    public class BRRHelper {
        static bool hasRanBefore = false;
        static MusicTrack addedTrack; 
        public static void AddMissingTrackToAudios(MusicTrack missedTrack) {
            if (!BombRushRadio.BombRushRadio.Audios.Contains(MusicCuratorPlugin.missingStageTrack)) {
                if (hasRanBefore) {BombRushRadio.BombRushRadio.Audios.Remove(addedTrack);}
                BombRushRadio.BombRushRadio.Audios.Insert(0, missedTrack); 
                hasRanBefore = true;
                addedTrack = missedTrack;
            }
        }
    }
}