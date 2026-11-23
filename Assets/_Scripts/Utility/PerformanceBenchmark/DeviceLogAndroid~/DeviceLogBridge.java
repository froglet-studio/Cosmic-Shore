package com.froglet.cosmicshore.devicelogs;

import android.app.job.JobInfo;
import android.app.job.JobScheduler;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.os.BatteryManager;
import android.os.Build;
import android.os.PersistableBundle;
import android.os.PowerManager;

import java.io.BufferedReader;
import java.io.File;
import java.io.FileOutputStream;
import java.io.FileReader;
import java.nio.charset.StandardCharsets;

/** The calls DeviceLogCapture.cs makes, and the token's one home. Docs/perf/DEVICE_LOGS.md. */
public final class DeviceLogBridge {
    static final int SEND_JOB_ID = 0x44455653;
    static final int VERIFY_JOB_ID = 0x44455656;
    static final String VERIFY_EXTRA = "verify";
    static final String TOKEN_FILE = "devlog_token";
    static final String USER_FILE = "devlog_user";
    static final String ROOT_FILE = "devlog_root";

    private DeviceLogBridge() {}

    /** Asks Android to run the sender once the network is up. It outlives this process; only a force stop cancels it. */
    public static void schedule(Context context, long delayMs) {
        if (read(context, TOKEN_FILE) == null) return;
        JobInfo.Builder info = new JobInfo.Builder(SEND_JOB_ID, new ComponentName(context, DeviceLogJob.class))
                .setRequiredNetworkType(JobInfo.NETWORK_TYPE_ANY)
                .setBackoffCriteria(30000, JobInfo.BACKOFF_POLICY_LINEAR);
        if (delayMs > 0) info.setMinimumLatency(delayMs);
        scheduler(context).schedule(info.build());
    }

    /** One check that GitHub takes the token, answered by a notification. */
    public static void verify(Context context) {
        PersistableBundle extras = new PersistableBundle();
        extras.putBoolean(VERIFY_EXTRA, true);
        scheduler(context).schedule(new JobInfo.Builder(VERIFY_JOB_ID, new ComponentName(context, DeviceLogJob.class))
                .setRequiredNetworkType(JobInfo.NETWORK_TYPE_ANY)
                .setExtras(extras)
                .build());
    }

    /** noBackupFilesDir: private to this app and left out of Android's cloud backup. */
    public static void setIdentity(Context context, String token, String user) throws Exception {
        write(context, TOKEN_FILE, token);
        write(context, USER_FILE, user);
    }

    public static void setUser(Context context, String user) throws Exception {
        write(context, USER_FILE, user);
    }

    public static void setDataRoot(Context context, String path) throws Exception {
        write(context, ROOT_FILE, path);
    }

    public static boolean hasToken(Context context) {
        return read(context, TOKEN_FILE) != null;
    }

    public static void forget(Context context) {
        new File(context.getNoBackupFilesDir(), TOKEN_FILE).delete();
    }

    public static int myPid() {
        return android.os.Process.myPid();
    }

    public static long rssKb() {
        try (BufferedReader r = new BufferedReader(new FileReader("/proc/self/status"))) {
            String line;
            while ((line = r.readLine()) != null) {
                if (line.startsWith("VmRSS:")) return Long.parseLong(line.replaceAll("[^0-9]", ""));
            }
        } catch (Exception ignored) {
            // an unreadable status file reads as -1 in the samples
        }
        return -1;
    }

    public static int thermalStatus(Context context) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) return -1;
        return ((PowerManager) context.getSystemService(Context.POWER_SERVICE)).getCurrentThermalStatus();
    }

    /** How close the phone is to severe throttling in 10 s: 1.0 is the line. Android rate-limits it; every 5 s is well inside. */
    public static float thermalHeadroom(Context context) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.R) return -1f;
        return ((PowerManager) context.getSystemService(Context.POWER_SERVICE)).getThermalHeadroom(10);
    }

    public static float batteryTempC(Context context) {
        Intent battery = context.registerReceiver(null, new IntentFilter(Intent.ACTION_BATTERY_CHANGED));
        return battery == null ? -1f : battery.getIntExtra(BatteryManager.EXTRA_TEMPERATURE, -10) / 10f;
    }

    private static JobScheduler scheduler(Context context) {
        return (JobScheduler) context.getSystemService(Context.JOB_SCHEDULER_SERVICE);
    }

    static String read(Context context, String name) {
        File f = new File(context.getNoBackupFilesDir(), name);
        if (!f.isFile()) return null;
        try (BufferedReader r = new BufferedReader(new FileReader(f))) {
            String s = r.readLine();
            return s == null || s.isEmpty() ? null : s;
        } catch (Exception e) {
            return null;
        }
    }

    private static void write(Context context, String name, String value) throws Exception {
        try (FileOutputStream out = new FileOutputStream(new File(context.getNoBackupFilesDir(), name))) {
            out.write(value.getBytes(StandardCharsets.UTF_8));
        }
    }
}
