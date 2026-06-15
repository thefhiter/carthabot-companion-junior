// RGBController.cpp
#include "RGBController.h"
#include <Adafruit_NeoPixel.h>
#include "../communs.h"

void RGBController::Init(){
  strip.begin(); 
  strip.show();            

}
RGBController::RGBController():strip(LedNumber, LedPin, NEO_GRB + NEO_KHZ800){

}
void RGBController::SetStaticColor(uint32_t color) {
    strip.fill(color);
    strip.show();  
}
void RGBController::colorWipe(uint32_t color) {
    strip.clear();
    for(int i =0; i < LedNumber;i++){
        strip.setPixelColor(i, color); //  Set pixel's color (in RAM)
    }
    strip.show();  

}
void RGBController::colorWave(uint32_t baseColor, float frequency, float speed) {
    if(speed == 0){
        for(int i = 0; i < strip.numPixels(); i++) {

            strip.setPixelColor(i, baseColor);
        }
        strip.show();
    }
    else
    {
        for(int i = 0; i < strip.numPixels(); i++) {
        // Calculate the phase shift based on position
        float phase = sin((millis() / 1000.0 * speed) + (i * frequency));
        // Adjust phase to range [0, 1]
        phase = (phase + 1.0) / 2.0;
        // Apply phase to color
        uint8_t r = (baseColor >> 16) * phase;
        uint8_t g = ((baseColor >> 8) & 0xFF) * phase;
        uint8_t b = (baseColor & 0xFF) * phase;
        // Set pixel color
        strip.setPixelColor(i, strip.Color(r, g, b));
        }
        strip.show();
    }

}
void RGBController::Execute(){

}

void RGBController::Clear(){
  strip.fill(0); // Fill all pixels with color black (off)
  strip.show();
}