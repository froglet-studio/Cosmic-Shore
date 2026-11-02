package com.froglet.cosmicshore.devicelogs;

import android.app.ActivityManager;
import android.app.ApplicationExitInfo;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.job.JobParameters;
import android.app.job.JobService;
import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.util.Base64;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.regex.Pattern;
import java.util.zip.GZIPOutputStream;

/**
 * Sends finished play sessions to the sender's bugledger-android branch and says so in a notification. It runs in its own
 * process (":reporter", set by DeviceLogGradleHook), so it starts no Unity and survives the game being swiped away.
 * A session is finished when the game process that wrote it is gone. Docs/perf/DEVICE_LOGS.md.
 */
public final class DeviceLogJob extends JobService {
    private static final String LOGS_FOLDER = "CosmicShore DeviceLogs";
    private static final String CAPTURES_FOLDER = "CosmicShore Diagnostics";
    private static final String LOADS_FOLDER = "Benchmarks/LoadInsights";
    private static final String SENT_PREFIX = "sent_";
    private static final String REPO = "froglet-studio/Cosmic-Shore";
    private static final String API = "https://api.github.com/repos/" + REPO;
    private static final String CHANNEL = "device_logs";
    private static final int MAX_LOG_BYTES = 8 * 1024 * 1024;
    private static final int MAX_TRACE_BYTES = 1024 * 1024;
    private static final int KEEP_SENT_SESSIONS = 5;
    private static final long CHECK_AGAIN_MS = 60000;
    private static final Pattern TOKEN_SHAPE = Pattern.compile("(ghp_|github_pat_)[A-Za-z0-9_]+");

    /** The token was refused: retrying cannot help until someone sets a new one. */
    private static final class Rejected extends IOException {
        private static final long serialVersionUID = 1L;
        Rejected(String message) { super(message); }
    }

    private volatile boolean stopped;

    @Override
    public boolean onStartJob(JobParameters params) {
        boolean verify = params.getExtras().getBoolean(DeviceLogBridge.VERIFY_EXTRA, false);
        new Thread(() -> {
            boolean retry = false;
            try {
                if (verify) verifyToken(); else run();
            } catch (Rejected e) {
                notifyUser("Device log not sent", "GitHub refused the token (" + e.getMessage() + "). Logs stay on the phone.", null);
            } catch (Exception e) {
                retry = true;
            }
            jobFinished(params, retry && !stopped);
        }, "DeviceLogJob").start();
        return true;
    }

    @Override
    public boolean onStopJob(JobParameters params) {
        stopped = true;
        return true;
    }

    private void verifyToken() throws Exception {
        String token = DeviceLogBridge.read(this, DeviceLogBridge.TOKEN_FILE);
        if (token == null) return;
        JSONObject repo = call(token, "GET", "", null);
        if (repo == null) throw new Rejected("it cannot see " + REPO);
        JSONObject permissions = repo.optJSONObject("permissions");
        if (permissions == null || !permissions.optBoolean("push", false)) throw new Rejected("it cannot write to " + REPO);
        notifyUser("Device logs are ready", "GitHub accepted the token. Play, swipe the game away, and the report sends itself.", null);
    }

    private void run() throws Exception {
        String token = DeviceLogBridge.read(this, DeviceLogBridge.TOKEN_FILE);
        String user = DeviceLogBridge.read(this, DeviceLogBridge.USER_FILE);
        if (token == null || user == null) return;

        File[] sessions = new File(dataRoot(), LOGS_FOLDER + "/pending").listFiles(File::isDirectory);
        if (sessions == null) return;
        Arrays.sort(sessions);

        boolean live = false;
        for (File session : sessions) {
            if (stopped) return;
            int pid = readPid(session);
            if (pid > 0 && isGameProcess(pid)) { live = true; continue; }
            send(session, pid, token, user);
        }
        // Backgrounded but still alive: it may be swiped later with no further signal, so look again until it is gone.
        if (live && !gameInForeground()) DeviceLogBridge.schedule(this, CHECK_AGAIN_MS);
    }

    private File dataRoot() {
        String path = DeviceLogBridge.read(this, DeviceLogBridge.ROOT_FILE);
        return path != null ? new File(path) : getExternalFilesDir(null);
    }

    private void send(File session, int pid, String token, String user) throws Exception {
        String id = session.getName();
        String headline = firstLine(new File(session, "summary.txt"));
        String branch = "bugledger-android/" + user;
        String folder = "devicelogs/" + user + "/" + id + "/";

        List<JSONObject> tree = new ArrayList<>();
        File[] files = session.listFiles(File::isFile);
        if (files != null) {
            for (File f : files) {
                String name = f.getName();
                if (name.equals("pid") || name.endsWith(".tmp")) continue;
                byte[] text = redact(readAll(f, Integer.MAX_VALUE));
                if (name.equals("unity_log.txt")) tree.add(file(token, folder + name + ".gz", gzip(text)));
                else tree.add(file(token, folder + name, text));
            }
        }
        if (pid > 0) {
            tree.add(file(token, folder + "logcat_app.txt.gz", gzip(redact(logcat("-v", "threadtime", "--pid=" + pid)))));
        }
        tree.add(file(token, folder + "logcat_crash.txt", redact(logcat("-b", "crash", "-v", "threadtime"))));
        addExitRecord(tree, pid, folder, token);

        // The same capture file the editor shares: under perf/ the daily sweep (perf_hunt.py collect) judges it as it is.
        long since = session.lastModified() - 86400000L;
        List<File> captures = capturesSince(since);
        for (File f : captures) {
            tree.add(file(token, "BugLedger/shared/reports/" + user + "-phone/perf/" + f.getName(), redact(readAll(f, Integer.MAX_VALUE))));
        }
        File[] loads = new File(dataRoot(), LOADS_FOLDER).listFiles((d, n) -> n.startsWith("load_") && n.endsWith(".txt"));
        if (loads != null) {
            Arrays.sort(loads);
            if (loads.length > 0) tree.add(file(token, folder + loads[loads.length - 1].getName(), readAll(loads[loads.length - 1], Integer.MAX_VALUE)));
        }

        commit(token, branch, tree, "devicelogs: " + id + " " + headline);

        for (File f : captures) f.renameTo(new File(f.getParentFile(), SENT_PREFIX + f.getName()));
        retire(session);
        notifyUser("Device log sent", headline, "https://github.com/" + REPO + "/tree/" + branch + "/" + folder);
    }

    // ---- what the app can read about itself after the fact ----

    private void addExitRecord(List<JSONObject> tree, int pid, String folder, String token) throws Exception {
        StringBuilder sb = new StringBuilder();
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            ActivityManager am = (ActivityManager) getSystemService(Context.ACTIVITY_SERVICE);
            for (ApplicationExitInfo e : am.getHistoricalProcessExitReasons(getPackageName(), 0, 16)) {
                if (e.getPid() != pid) continue;
                sb.append(e).append('\n');
                try (InputStream trace = e.getTraceInputStream()) {
                    if (trace != null) tree.add(file(token, folder + "exit_trace.txt", readStream(trace, MAX_TRACE_BYTES)));
                }
            }
        }
        if (sb.length() == 0) sb.append("Android kept no exit record for pid ").append(pid).append('\n');
        tree.add(file(token, folder + "exit.txt", sb.toString().getBytes(StandardCharsets.UTF_8)));
    }

    /** logcat run by this app shows only this app's lines, so nothing of any other app can reach the repo. The newest part is kept. */
    private static byte[] logcat(String... options) {
        List<String> command = new ArrayList<>();
        command.add("logcat");
        command.add("-d");
        command.addAll(Arrays.asList(options));
        Process p = null;
        try {
            p = new ProcessBuilder(command).redirectErrorStream(true).start();
            byte[] all = readStream(p.getInputStream(), Integer.MAX_VALUE);
            return all.length <= MAX_LOG_BYTES ? all : Arrays.copyOfRange(all, all.length - MAX_LOG_BYTES, all.length);
        } catch (IOException e) {
            return ("logcat failed: " + e.getMessage()).getBytes(StandardCharsets.UTF_8);
        } finally {
            if (p != null) p.destroy();
        }
    }

    private static byte[] redact(byte[] text) {
        return TOKEN_SHAPE.matcher(new String(text, StandardCharsets.UTF_8)).replaceAll("[token removed]").getBytes(StandardCharsets.UTF_8);
    }

    private static byte[] gzip(byte[] data) throws IOException {
        ByteArrayOutputStream out = new ByteArrayOutputStream(data.length / 4 + 64);
        try (GZIPOutputStream gz = new GZIPOutputStream(out)) {
            gz.write(data);
        }
        return out.toByteArray();
    }

    private List<File> capturesSince(long since) {
        List<File> out = new ArrayList<>();
        File[] all = new File(dataRoot(), CAPTURES_FOLDER).listFiles((d, n) -> n.startsWith("capture_") && n.endsWith(".json"));
        if (all != null) for (File f : all) if (f.lastModified() >= since) out.add(f);
        return out;
    }

    private boolean gameInForeground() {
        List<ActivityManager.RunningAppProcessInfo> running = ((ActivityManager) getSystemService(Context.ACTIVITY_SERVICE)).getRunningAppProcesses();
        if (running == null) return false;
        for (ActivityManager.RunningAppProcessInfo p : running) {
            if (p.processName.equals(getPackageName()) && p.importance <= ActivityManager.RunningAppProcessInfo.IMPORTANCE_FOREGROUND) return true;
        }
        return false;
    }

    private boolean isGameProcess(int pid) {
        File cmdline = new File("/proc/" + pid + "/cmdline");
        if (!cmdline.exists()) return false;
        try {
            return new String(readAll(cmdline, 256), StandardCharsets.UTF_8).trim().equals(getPackageName());
        } catch (IOException e) {
            return false;
        }
    }

    private static int readPid(File session) {
        try {
            return Integer.parseInt(firstLine(new File(session, "pid")).trim());
        } catch (NumberFormatException e) {
            return -1;
        }
    }

    /** Sent sessions move aside rather than vanish, so a cable can still pull the newest few; older ones go. */
    private void retire(File session) {
        File sent = new File(session.getParentFile().getParentFile(), "sent");
        sent.mkdirs();
        session.renameTo(new File(sent, session.getName()));
        File[] kept = sent.listFiles(File::isDirectory);
        if (kept == null) return;
        Arrays.sort(kept);
        for (int i = 0; i < kept.length - KEEP_SENT_SESSIONS; i++) {
            File[] files = kept[i].listFiles();
            if (files != null) for (File f : files) f.delete();
            kept[i].delete();
        }
    }

    // ---- GitHub: one commit per session ----

    private static JSONObject file(String token, String path, byte[] content) throws Exception {
        JSONObject blob = new JSONObject();
        blob.put("content", Base64.encodeToString(content, Base64.NO_WRAP));
        blob.put("encoding", "base64");
        JSONObject item = new JSONObject();
        item.put("path", path);
        item.put("mode", "100644");
        item.put("type", "blob");
        item.put("sha", call(token, "POST", "/git/blobs", blob).getString("sha"));
        return item;
    }

    private static void commit(String token, String branch, List<JSONObject> items, String message) throws Exception {
        JSONObject ref = call(token, "GET", "/git/ref/heads/" + branch, null);
        String parent = ref == null ? null : ref.getJSONObject("object").getString("sha");

        JSONObject treeBody = new JSONObject();
        treeBody.put("tree", new JSONArray(items));
        if (parent != null) treeBody.put("base_tree", call(token, "GET", "/git/commits/" + parent, null).getJSONObject("tree").getString("sha"));
        String tree = call(token, "POST", "/git/trees", treeBody).getString("sha");

        JSONObject commitBody = new JSONObject();
        commitBody.put("message", message);
        commitBody.put("tree", tree);
        JSONArray parents = new JSONArray();
        if (parent != null) parents.put(parent);
        commitBody.put("parents", parents);
        String sha = call(token, "POST", "/git/commits", commitBody).getString("sha");

        JSONObject refBody = new JSONObject();
        refBody.put("sha", sha);
        if (parent != null) {
            call(token, "PATCH", "/git/refs/heads/" + branch, refBody);
        } else {
            refBody.put("ref", "refs/heads/" + branch);
            call(token, "POST", "/git/refs", refBody);
        }
    }

    /** The parsed answer, or null on a 404 from a GET. */
    private static JSONObject call(String token, String method, String path, JSONObject body) throws Exception {
        HttpURLConnection c = (HttpURLConnection) new URL(API + path).openConnection();
        try {
            // HttpURLConnection has no PATCH verb; GitHub takes this override on a POST.
            c.setRequestMethod(method.equals("PATCH") ? "POST" : method);
            if (method.equals("PATCH")) c.setRequestProperty("X-HTTP-Method-Override", "PATCH");
            c.setConnectTimeout(20000);
            c.setReadTimeout(60000);
            c.setRequestProperty("Authorization", "Bearer " + token);
            c.setRequestProperty("Accept", "application/vnd.github+json");
            c.setRequestProperty("User-Agent", "CosmicShore-DeviceLog");
            if (body != null) {
                c.setDoOutput(true);
                c.setRequestProperty("Content-Type", "application/json");
                try (OutputStream out = c.getOutputStream()) {
                    out.write(body.toString().getBytes(StandardCharsets.UTF_8));
                }
            }
            int code = c.getResponseCode();
            if (code == 404 && method.equals("GET")) return null;
            // A token that cannot write gets a 404 on a POST, not a 403, so a retry would never end.
            if (code == 401 || code == 403 || code == 404) throw new Rejected("HTTP " + code);
            if (code < 200 || code >= 300) throw new IOException(method + " " + path + " answered HTTP " + code);
            return new JSONObject(new String(readStream(c.getInputStream(), Integer.MAX_VALUE), StandardCharsets.UTF_8));
        } finally {
            c.disconnect();
        }
    }

    // ---- notification ----

    private void notifyUser(String title, String text, String url) {
        NotificationManager nm = (NotificationManager) getSystemService(Context.NOTIFICATION_SERVICE);
        nm.createNotificationChannel(new NotificationChannel(CHANNEL, "Device logs", NotificationManager.IMPORTANCE_DEFAULT));
        Notification.Builder n = new Notification.Builder(this, CHANNEL)
                .setSmallIcon(android.R.drawable.stat_sys_upload_done)
                .setContentTitle(title)
                .setContentText(text)
                .setStyle(new Notification.BigTextStyle().bigText(text))
                .setAutoCancel(true);
        if (url != null) {
            n.setContentIntent(PendingIntent.getActivity(this, 0, new Intent(Intent.ACTION_VIEW, Uri.parse(url)),
                    PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT));
        }
        nm.notify((int) (System.currentTimeMillis() & 0xFFFF), n.build());
    }

    // ---- files ----

    private static String firstLine(File f) {
        try (BufferedReader r = new BufferedReader(new InputStreamReader(new FileInputStream(f), StandardCharsets.UTF_8))) {
            String s = r.readLine();
            return s == null ? "" : s;
        } catch (IOException e) {
            return "";
        }
    }

    private static byte[] readAll(File f, int max) throws IOException {
        try (InputStream in = new FileInputStream(f)) {
            return readStream(in, max);
        }
    }

    private static byte[] readStream(InputStream in, int max) throws IOException {
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        byte[] buffer = new byte[16384];
        int n;
        while (out.size() < max && (n = in.read(buffer)) > 0) out.write(buffer, 0, Math.min(n, max - out.size()));
        return out.toByteArray();
    }
}
