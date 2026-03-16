import socket
import threading
import queue
import time

# 클라이언트의 연결할 IP 주소와 포트 번호
server_ip = "localhost"
ports = [7701]
# msg_list = ["$LIGHT:1,1@", 
#             "$TEST:1,1,BJWC73.20@", "$TEST:1,3,BJWC73.20@", "$TEST:1,4,BJWC73.20@", 
#             "$TEST:2,1,BJWC73.20@", 
#             "$TEST:3,1,BJWC73.20@", 
#             "$TEST:4,1,BJWC73.20@", "$TEST:4,2,BJWC73.20@", 
#             "$TEST:5,1,BJWC73.20@", "$TEST:5,2,BJWC73.20@",
#             "$SITE_STATUS:1@",
#            "$RECIPE:1,recipe@",
#             "$GET_RECIPE:1,10,1@"
#            ]
msg_list = [
            "$TEST:1,3,BJWC73.20@"
           ]
error_count = 0

# TCP 클라이언트 함수
def tcp_client(port):
    try:
        # 소켓 생성 및 연결
        client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        client_socket.connect((server_ip, port))
    except socket.timeout:
        print(f"Port {port}: Response timeout")
    except Exception as e:
        print(f"Port {port}: {e}")
    
    while True:
        try:
            for msg in msg_list:
#                 if error_count > 10:
#                     break
                print(msg)
                # 서버로 메시지 전송
                client_socket.send(msg.encode('utf-8'))
                print(f"Port[{port}] Send data: {msg}")
                # 서버로부터 응답 받기 (2초 내에 응답이 오지 않으면 예외 발생)
                client_socket.settimeout(5)
                response = client_socket.recv(1024)
                print(f"Port[{port}] Receive data: {response.decode('utf-8')}")
                time.sleep(2)
            break
        except socket.timeout:
            print(f"Port {port}: Response timeout")
            break
        except Exception as e:
            #print(f"Port {port}: {e}")
            break

    
    
# 메인 함수
def main():
    # 스레드 리스트 생성
    threads = []

    # 각 포트에 대해 스레드를 생성하여 실행
    for port in ports:
        thread = threading.Thread(target=tcp_client, args=(port,))
        thread.start()
        threads.append(thread)

    # 모든 스레드가 완료될 때까지 대기
    for thread in threads:
        thread.join()
        
    print("thread end")

if __name__ == "__main__":
    main()