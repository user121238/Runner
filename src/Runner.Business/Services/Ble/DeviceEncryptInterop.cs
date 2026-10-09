using System.Security.Cryptography;
using Android.Runtime;

namespace Runner.Business.Services.Ble;

public static class DeviceEncryptInterop
{
    private static bool _loaded;
    private static readonly object _lock = new();
    private static IntPtr _classRef;
    private static IntPtr _instance;

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        lock (_lock)
        {
            if (_loaded) return;
            var systemClass = JNIEnv.FindClass("java/lang/System");
            var loadLib = JNIEnv.GetStaticMethodID(systemClass, "loadLibrary", "(Ljava/lang/String;)V");
            var libName = JNIEnv.NewString("device-encrypt");
            JNIEnv.CallStaticVoidMethod(systemClass, loadLib, new JValue(libName));
            JNIEnv.DeleteLocalRef(libName);

            _classRef = JNIEnv.FindClass("com/huami/core/device/feature/encrypt/impl/KeyShareApiImpl");
            var ctor = JNIEnv.GetMethodID(_classRef, "<init>", "()V");
            var localInstance = JNIEnv.NewObject(_classRef, ctor);
            _instance = JNIEnv.NewGlobalRef(localInstance);
            JNIEnv.DeleteLocalRef(localInstance);
            _loaded = true;
        }
    }

    public static byte[] GeneratePrivateKey()
    {
        var key = new byte[24];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    public static byte[]? GeneratePublicKey(byte[] privateKey)
    {
        if (privateKey.Length != 24) return null;
        EnsureLoaded();

        var methodId = JNIEnv.GetMethodID(_classRef, "generatePublicKeyExternal", "([B)[B");
        var keyJni = JNIEnv.NewArray(privateKey);
        var resultJni = JNIEnv.CallObjectMethod(_instance, methodId, new JValue(keyJni));
        JNIEnv.DeleteLocalRef(keyJni);

        if (resultJni == IntPtr.Zero) return null;
        var result = (byte[])JNIEnv.GetArray(resultJni, JniHandleOwnership.DoNotTransfer);
        JNIEnv.DeleteLocalRef(resultJni);
        return result;
    }

    public static byte[]? GenerateSharedSecret(byte[] privateKey, byte[] publicKey)
    {
        if (privateKey.Length != 24 || publicKey.Length != 48) return null;
        EnsureLoaded();

        var methodId = JNIEnv.GetMethodID(_classRef, "generatePrivateKeyExternal", "([B[B)[B");
        var privJni = JNIEnv.NewArray(privateKey);
        var pubJni = JNIEnv.NewArray(publicKey);
        var resultJni = JNIEnv.CallObjectMethod(_instance, methodId, new JValue(privJni), new JValue(pubJni));
        JNIEnv.DeleteLocalRef(privJni);
        JNIEnv.DeleteLocalRef(pubJni);

        if (resultJni == IntPtr.Zero) return null;
        var result = (byte[])JNIEnv.GetArray(resultJni, JniHandleOwnership.DoNotTransfer);
        JNIEnv.DeleteLocalRef(resultJni);
        return result;
    }
}
