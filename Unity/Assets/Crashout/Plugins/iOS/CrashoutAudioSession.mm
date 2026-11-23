#import <AVFoundation/AVFoundation.h>
#import <Foundation/Foundation.h>

extern "C" {

void CrashoutActivateAudioSession(void) {
    AVAudioSession *session = [AVAudioSession sharedInstance];
    NSError *error = nil;
    // Use Playback so game audio plays even when the iPhone hardware silent switch is enabled,
    // and mix with others so background audio / ARKit camera initialization does not silence it.
    AVAudioSessionCategoryOptions options =
        AVAudioSessionCategoryOptionMixWithOthers |
        AVAudioSessionCategoryOptionDefaultToSpeaker;
    [session setCategory:AVAudioSessionCategoryPlayback withOptions:options error:&error];
    if (error != nil) {
        NSLog(@"[CrashoutAudio] setCategory error: %@", error.localizedDescription);
        error = nil;
    }
    [session setActive:YES error:&error];
    if (error != nil) {
        NSLog(@"[CrashoutAudio] setActive error: %@", error.localizedDescription);
    }
}

}
