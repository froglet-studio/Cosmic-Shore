package com.froglet.cosmicshore.playgames;

import android.app.Activity;

import com.google.android.gms.games.GamesSignInClient;
import com.google.android.gms.games.PlayGames;
import com.google.android.gms.games.PlayGamesSdk;

/**
 * Gets a Play Games server auth code for UGS sign-in (fair play, Threat Board T1). Called from
 * Assets/_Scripts/System/PlatformSignIn/PlayGamesPlatform.cs. Never shows a sign-in screen: it asks only whether
 * Play Games' own automatic sign-in succeeded, and reads nothing but that answer and the auth code.
 */
public final class PlayGamesSignInBridge {

    /** Answered exactly once: an auth code, or null with a short reason. */
    public interface Callback {
        void onResult(String authCode, String error);
    }

    private PlayGamesSignInBridge() {}

    public static void requestServerAuthCode(final Activity activity, final String serverClientId,
                                             final Callback callback) {
        activity.runOnUiThread(() -> {
            try {
                PlayGamesSdk.initialize(activity);
                final GamesSignInClient client = PlayGames.getGamesSignInClient(activity);
                client.isAuthenticated().addOnCompleteListener(auth -> {
                    if (!auth.isSuccessful() || auth.getResult() == null || !auth.getResult().isAuthenticated()) {
                        callback.onResult(null, "not_signed_in");
                        return;
                    }
                    client.requestServerSideAccess(serverClientId, false).addOnCompleteListener(code -> {
                        if (code.isSuccessful() && code.getResult() != null)
                            callback.onResult(code.getResult(), null);
                        else
                            callback.onResult(null, "auth_code_failed");
                    });
                });
            } catch (RuntimeException e) {
                callback.onResult(null, "init_failed");
            }
        });
    }
}
