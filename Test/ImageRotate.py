import cv2 as cv
import tkinter as tk
from tkinter import filedialog
import os
import sys
# from tkinter import messagebox

par = ''
if len(sys.argv) > 1:
    par = sys.argv[1]

def nothing(x):
    pass

cv.namedWindow('Rotate', cv.WINDOW_NORMAL)

cv.createTrackbar('rotate', 'Rotate', 0, 360, nothing)

cv.setTrackbarPos('rotate', 'Rotate', 0)

# tkinter 초기화
root = tk.Tk()
root.withdraw()  # 메인 윈도우를 숨깁니다.

# 파일 대화 상자 열기
file_path = filedialog.askopenfilename()

if par != '':
    file_path = par

img_input = cv.imread(file_path) 

img = img_input
height, width, channel = img.shape
img_rot = img
rot = 0

while (1):  
    rot = cv.getTrackbarPos('rotate', 'Rotate')

    matrix = cv.getRotationMatrix2D((width/2, height/2), rot, 1)
    img_rot = cv.warpAffine(img, matrix, (width, height))

    # img_rot = cv.bitwise_not(img)

    cv.imshow('Rotate', img_rot)

    if cv.waitKey(1) & 0xFF == 27:
        break

new_path = '%s/%s_%ddegree.bmp' % (os.path.dirname(file_path), os.path.splitext(os.path.basename(file_path))[0], rot)
img_gray = cv.cvtColor(img_rot, cv.COLOR_BGR2GRAY)
cv.imwrite(new_path, img_gray)
cv.destroyAllWindows()