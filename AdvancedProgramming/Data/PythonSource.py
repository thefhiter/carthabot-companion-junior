import machine
import neopixel
import time

# Config
NUM_LEDS = 11
PIN = 21
np = neopixel.NeoPixel(machine.Pin(PIN), NUM_LEDS)

# === Helper Functions ===
def wheel(pos):
    """Generate color from position (0-255)."""
    if pos < 85:
        return (255 - pos * 3, pos * 3, 0)
    elif pos < 170:
        pos -= 85
        return (0, 255 - pos * 3, pos * 3)
    else:
        pos -= 170
        return (pos * 3, 0, 255 - pos * 3)

# === Effect State ===
offset = 0
chase_pos = 0
last_change = time.ticks_ms()

# === Effect Update Functions ===
def rainbow_wave_step():
    """Rainbow flowing across strip."""
    global offset
    for i in range(NUM_LEDS):
        color_index = (int((i * 256 / NUM_LEDS) + offset) % 256)
        np[i] = wheel(color_index)
    np.write()
    offset = (offset + 3) % 256

def comet_step():
    """Chasing pixel with colorful tail."""
    global chase_pos
    np.fill((0,0,0))  # clear strip

    # Comet length
    length = 4  

    for i in range(length):
        pos = (chase_pos - i) % NUM_LEDS
        color = wheel((chase_pos * 20 + i * 40) % 256)  # different color per tail step
        # fade tail brightness
        fade = max(0, 255 - i * 60)
        faded_color = tuple((c * fade) // 255 for c in color)
        np[pos] = faded_color

    np.write()
    chase_pos = (chase_pos + 1) % NUM_LEDS

# === Main Loop ===
effects = [rainbow_wave_step, comet_step]
current = 0
last_effect_switch = time.ticks_ms()

while True:
    if current == 0:
        rainbow_wave_step()
        time.sleep(0.02)
    elif current == 1:
        comet_step()
        time.sleep(0.05)

    # Switch effect every 4 seconds
    if time.ticks_diff(time.ticks_ms(), last_effect_switch) > 4000:
        current = (current + 1) % len(effects)
        last_effect_switch = time.ticks_ms()
