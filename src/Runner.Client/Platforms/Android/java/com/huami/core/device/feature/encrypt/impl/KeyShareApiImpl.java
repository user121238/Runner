package com.huami.core.device.feature.encrypt.impl;

public class KeyShareApiImpl {
    static { System.loadLibrary("device-encrypt"); }

    public native byte[] generatePublicKeyExternal(byte[] privateKey);
    public native byte[] generatePrivateKeyExternal(byte[] privateKey, byte[] publicKey);
}
