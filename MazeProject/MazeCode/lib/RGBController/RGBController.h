// RGBController.h
#ifndef RGBController_h
#define RGBController_h
#include <Arduino.h>
#include <Adafruit_NeoPixel.h>

class RGBController{
  private:
    int pixelInterval = 10; 
    bool RunOneTime = false;   
    void colorWipe(uint32_t color );
    void colorWave(uint32_t baseColor, float frequency, float speed);
  public:
    RGBController();
    void Init();
    void Execute();
    void Clear();
    void SetStaticColor(uint32_t color);
    Adafruit_NeoPixel strip;

};

#endif