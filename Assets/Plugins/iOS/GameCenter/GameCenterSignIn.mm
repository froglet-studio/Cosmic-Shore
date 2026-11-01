// Game Center identity for UGS sign-in (fair play, Threat Board T1; Docs/AntiCheat/README.md §4.1).
// Called from Assets/_Scripts/System/PlatformSignIn/GameCenterPlatform.cs. Reads nothing but the local player's
// Game Center identity-verification signature, which Apple issues for exactly this purpose.
#import <Foundation/Foundation.h>
#import <GameKit/GameKit.h>

typedef void (*CSGameCenterCallback)(int requestId, const char* teamPlayerId, const char* publicKeyUrl,
                                     const char* signature, const char* salt, unsigned long long timestamp,
                                     const char* error);

static void CSGameCenterFetch(int requestId, CSGameCenterCallback callback)
{
    GKLocalPlayer* player = GKLocalPlayer.localPlayer;
    [player fetchItemsForIdentityVerificationSignature:^(NSURL* publicKeyURL, NSData* signature, NSData* salt,
                                                          uint64_t timestamp, NSError* error) {
        if (error != nil || publicKeyURL == nil || signature == nil || salt == nil)
        {
            callback(requestId, NULL, NULL, NULL, NULL, 0, "signature_failed");
            return;
        }
        callback(requestId,
                 [player.teamPlayerID UTF8String],
                 [publicKeyURL.absoluteString UTF8String],
                 [[signature base64EncodedStringWithOptions:0] UTF8String],
                 [[salt base64EncodedStringWithOptions:0] UTF8String],
                 timestamp, NULL);
    }];
}

extern "C" void CS_GameCenterSignIn(int requestId, CSGameCenterCallback callback)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        GKLocalPlayer* player = GKLocalPlayer.localPlayer;
        if (player.isAuthenticated)
        {
            CSGameCenterFetch(requestId, callback);
            return;
        }

        // GameKit calls this handler again whenever the player's state changes; only the first answer counts.
        __block BOOL answered = NO;
        player.authenticateHandler = ^(UIViewController* viewController, NSError* error) {
            if (answered) return;
            answered = YES;
            if (GKLocalPlayer.localPlayer.isAuthenticated)
                CSGameCenterFetch(requestId, callback);
            else
                // A view controller means the player is not signed in to Game Center on this device. It is not
                // shown: Game Center never stands between a player and the game.
                callback(requestId, NULL, NULL, NULL, NULL, 0, viewController != nil ? "not_signed_in" : "auth_failed");
        };
    });
}
