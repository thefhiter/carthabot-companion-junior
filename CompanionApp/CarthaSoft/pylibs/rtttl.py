from machine import Pin, PWM
import neopixel
import time
np = neopixel.NeoPixel(Pin(21), 11)

while True:
    np[0] = (255, 0, 0)
    np.write()
    sleep_us(100)
    np[0] = (255,255, 255)
    np.write()
    sleep_us(100)