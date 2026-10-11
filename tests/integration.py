#!/usr/bin/env python3
"""Exercise the real TCP server. --audio additionally verifies the virtual microphone."""
import argparse
import math
import pathlib
import select
import socket
import struct
import subprocess
import time

ROOT = pathlib.Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--audio', action='store_true')
args = parser.parse_args()
binary = ROOT / 'build/mac/MicBridge.app/Contents/MacOS/MicBridge'

def line(sock):
    value = bytearray()
    while len(value) < 256:
        b = sock.recv(1)
        if not b: break
        value.extend(b)
        if b == b'\n': break
    return bytes(value)

def connect(hello=b'MICBRIDGE2\n'):
    sock = socket.create_connection(('127.0.0.1', 18769), timeout=3)
    sock.sendall(hello)
    return sock, line(sock)

def control(sock, number, expected):
    sock.sendall(struct.pack('<H', number))
    reply = line(sock)
    assert reply == expected, reply

server_args = [str(binary), '--test-server'] + (['--with-audio'] if args.audio else [])
server = subprocess.Popen(server_args, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
probe = None
try:
    for _ in range(50):
        try:
            sock, response = connect(b'UUMIC1 12345678\n')
            break
        except OSError: time.sleep(.1)
    else: raise AssertionError('receiver did not start')
    assert response.startswith(b'ERROR '), response
    sock.close()
    sock, response = connect()
    assert response == b'OK\n', response
    second, response = connect()
    assert response.startswith(b'ERROR '), response
    second.close()
    # Connected idle must stay silent and survive beyond the normal timeout.
    for _ in range(6):
        control(sock, 65535, b'PONG\n')
        time.sleep(1)
    control(sock, 65534, b'STARTED\n')
    if args.audio:
        check = ROOT / 'build/mac/AudioProbe'
        subprocess.run(['swiftc','-swift-version','5','-module-cache-path',str(ROOT/'build/swift-cache'),
                        str(ROOT/'src/mac/Audio.swift'),str(ROOT/'tests/audio-probe/main.swift'),'-o',str(check)],check=True)
        probe = subprocess.Popen([str(check)], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        assert select.select([probe.stdout], [], [], 8)[0], 'audio probe did not become ready'
        assert probe.stdout.readline().strip() == 'READY', 'audio probe could not start'
    for block in range(100):
        pcm = b''.join(struct.pack('<h', int(3276*math.sin(2*math.pi*997*(block*960+i)/48000))) for i in range(960))
        # Split packet headers too: TCP packet boundaries have no meaning to the decoder.
        frame = struct.pack('<H',len(pcm)) + pcm
        sock.sendall(frame[:1]); sock.sendall(frame[1:])
        time.sleep(.02)
    control(sock, 0, b'STOPPED\n')
    if probe:
        out, err = probe.communicate(timeout=8)
        assert probe.returncode == 0, err
        print(out.strip())
    for _ in range(10):
        control(sock, 65534, b'STARTED\n')
        sock.sendall(b'\x02\x00\x00\x00')
        control(sock, 0, b'STOPPED\n')
    # Audio after release is rejected, rather than silently played.
    sock.sendall(b'\x02\x00\xff\x7f')
    assert line(sock).startswith(b'ERROR ')
    sock.close(); time.sleep(.15)
    for _ in range(10):
        sock, response = connect(); assert response == b'OK\n', response
        sock.close(); time.sleep(.1)
    sock, response = connect(); assert response == b'OK\n'
    sock.sendall(b'\x01\x00'); assert line(sock).startswith(b'ERROR '); sock.close()
    print('PASS: old version rejection, single peer, idle keepalive, fragmented audio, ten press/release cycles, post-release rejection, ten reconnects and invalid frame bounds')
finally:
    if probe and probe.poll() is None: probe.terminate(); probe.wait(timeout=5)
    server.terminate(); server.wait(timeout=5)
