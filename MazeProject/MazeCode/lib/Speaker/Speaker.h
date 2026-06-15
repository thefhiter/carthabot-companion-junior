// Speaker.h
#ifndef Speaker_h
#define Speaker_h
#include <Arduino.h>


class Speaker {
  private:
  public:
    Speaker();
    void PlayChangeMode();
    void PlayCanChangeMode();
    void PlayCantChangeMode();
};

#endif