import firebase_admin
from firebase_admin import credentials
from firebase_admin import db
from gpiozero import Robot
import gpiozero
import time
import RPi.GPIO as IO

IO.setwarnings(False)
IO.setmode(IO.BCM)

IO.setup(23,IO.IN) #GPIO 23 -> Left IR sensor as input
IO.setup(24,IO.IN) #GPIO 24 -> Right IR sensor as input

########################################################################
robby = Robot(left=(7,8), right=(9,10))

def firebase_data():
    cred = credentials.Certificate("major-final-firebase.json")
    firebase_admin.initialize_app(cred, {
    'databaseURL': 'https://major-final.firebaseio.com/'})

    ref = db.reference('/')
    data = ref.get()
    
    return data

##########################################################################
def bot():
    data = firebase_data()
    if data == None:
        print("No Data found in Firebase")
        val = 0
        robby.stop()
    
    else:
        val = int(data['set'])
        print("Size: {}".format(val))
  
    if val == 0:
        robby.stop()
    
    else:
        data['angle'].append('0.0')
        for a, d in zip(data['angle'], data['cmd']):
            if float(a) != 0.0:
                print(a)
                moveAngle(float(a))
            if float(d) != 0.0:
                print(d)
                forwardDistance(float(d))
            
##########################################################################        
def moveAngle(a):
#4.090 - per count
    if(a > 0):
        count = a//4.090
        print(count)
        while count > 0:
            robby.right()
            flag = 0
            lir = IO.input(23)
            rir = IO.input(24)
            if lir == True and flag == 0:
                flag = 1
                count -= 1
            if lir == False and flag == 1:
                flag = 0
            if rir == True and flag == 0:
                flag = 1
                count -= 1
            if rir == False and flag == 1:
                flag = 0
    else:
        count = -a//4.090
        print(count)
        while count > 0:
            robby.left()
            flag = 0
            lir = IO.input(23)
            rir = IO.input(24)
            if lir == True and flag == 0:
                flag = 1
                count -= 1
            if lir == False and flag == 1:
                flag = 0
            if rir == True and flag == 0:
                flag = 1
                count -= 1
            if rir == False and flag == 1:
                flag = 0
    robby.stop()

##########################################################################
def forwardDistance(d):
#5.44 mm per count
    count = d*10//5.44
    print(count)
    while count > 0:
            robby.forward()
            flag = 0
            lir = IO.input(23)
            rir = IO.input(24)
            if lir == True and flag == 0:
                flag = 1
                count -= 1
            if lir == False and flag == 1:
                flag = 0
            if rir == True and flag == 0:
                flag = 1
                count -= 1
            if rir == False and flag == 1:
                flag = 0
    robby.stop()

bot()