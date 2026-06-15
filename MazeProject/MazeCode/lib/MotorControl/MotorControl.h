
// MotorControl.h
#ifndef MotorControl_h
#define MotorControl_h
#include <Arduino.h>



class MotorControl {
  private:

  public:
    MotorControl();
    void Init();
    void Stop();
    void GoForward();
    void GoBack();
    void TurnLeft();
    void TurnRight();
    void TurnFullLeft();
    void TurnFullRight();
    void TurnRightRight();
    void TurnLeftLeft();
    int speed = 150;
};

#endif
