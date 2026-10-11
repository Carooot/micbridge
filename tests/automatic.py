#!/usr/bin/env python3
"""BlackHole required: real input demand must start capture; output must not keep it alive."""
import pathlib
import select
import socket
import subprocess
import time

ROOT = pathlib.Path(__file__).resolve().parents[1]
binary = ROOT/'build/mac/MicBridge.app/Contents/MacOS/MicBridge'
probe_binary = ROOT/'build/mac/AudioProbe'
subprocess.run(['swiftc', '-module-cache-path', str(ROOT/'build/swift-cache'),
                str(ROOT/'src/mac/Audio.swift'), str(ROOT/'tests/audio-probe/main.swift'), '-o', str(probe_binary)], check=True)
server = subprocess.Popen([str(binary), '--test-server', '--test-follow', '--with-audio'], stderr=subprocess.PIPE)
probe = None
sock = None
def line():
    out = b''
    while len(out) < 256:
        b = sock.recv(1)
        if not b: break
        out += b
        if b == b'\n': break
    return out
try:
    for _ in range(50):
        try:
            sock = socket.create_connection(('127.0.0.1',18769),timeout=4)
            break
        except OSError: time.sleep(.1)
    else: raise AssertionError('server did not start')
    sock.sendall(b'MICBRIDGE3\n'); assert line() == b'OK\n'
    assert line() == b'CAPTURE 0\n', 'test requires no other app recording BlackHole'
    for _ in range(3):
        probe = subprocess.Popen([str(probe_binary),'--usage-only'],stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
        assert probe.stdout.readline().strip() == 'READY'
        start = time.monotonic()
        assert line() == b'CAPTURE 1\n'
        latency = time.monotonic() - start
        assert latency < .8, latency
        sock.sendall(b'\xfe\xff'); assert line() == b'STARTED\n'
        out, err = probe.communicate(timeout=5); assert probe.returncode == 0, err
        # The receiver's BlackHole OUTPUT is still open here. Only INPUT should drive demand.
        assert line() == b'CAPTURE 0\n', 'output feedback kept capture alive'
        sock.sendall(b'\x00\x00'); assert line() == b'STOPPED\n'
        sock.sendall(b'\xff\xff'); assert line() == b'PONG\n'
        assert not select.select([sock], [], [], .4)[0], 'capture reopened without input'
        print(f'PASS: input-driven start/stop; notification in {latency:.3f}s; no output feedback')
finally:
    if sock: sock.close()
    if probe and probe.poll() is None: probe.terminate(); probe.wait(timeout=5)
    server.terminate(); server.wait(timeout=5)
