import socket


HOST = "127.0.0.1"
PORT = 7701


def main():
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server:
        server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server.bind((HOST, PORT))
        server.listen(1)
        print(f"LISTENING {HOST}:{PORT}", flush=True)

        conn, addr = server.accept()
        with conn:
            print(f"CONNECTED {addr[0]}:{addr[1]}", flush=True)
            data = conn.recv(1024)
            message = data.decode("utf-8", errors="replace")
            print(f"RECV {message}", flush=True)

            response = "$TEST:1,2,1,OK,0.100,0.200,0.300@"
            conn.sendall(response.encode("utf-8"))
            print(f"SEND {response}", flush=True)


if __name__ == "__main__":
    main()
