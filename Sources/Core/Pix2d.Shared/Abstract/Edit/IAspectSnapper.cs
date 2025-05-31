namespace Pix2d.Services;

public interface IAspectSnapper
{
    bool IsAspectLocked { get; }
    bool ForceAspectLock { get; set; }

    bool DrawFromCenterLocked { get; }
    bool ForceDrawFromCenterAspectLock { get; set; }

}