import socket


HOST = "127.0.0.1"
PORT = 7701
MESSAGE = "$TEST:1,2,BJWC73.20@"


def main():
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as client:
        client.connect((HOST, PORT))
        client.sendall(MESSAGE.encode("utf-8"))
        print(f"SEND {MESSAGE}", flush=True)
        response = client.recv(1024).decode("utf-8", errors="replace")
        print(f"RECV {response}", flush=True)


if __name__ == "__main__":
    main()
