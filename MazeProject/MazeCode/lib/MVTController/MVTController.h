// MVTController.h
#ifndef MVTController_h
#define MVTController_h
#include <RGBController.h>
#include <Arduino.h>
#include "../communs.h"
#include <Speaker.h>
#include <MotorControl.h>

enum mvt{
  Empty,
  Forward,
  Backward,
  Left,
  Right,
};

enum mode{
    Idle,
    Runing,
    Done,
    Learning,
    LearningSerial
};

class MVTController{
  private:
    mvt mvtTable[MaxMouvment] = {};
    int curentExcecStep = 0;
    int curentSaveStep = 0;
    RGBController rgb;
    Speaker sound;
    MotorControl motors;
  public:
    mode WorkingMode = Idle;
    MVTController();
    void Init();
    void Execute();
    void ClearTable();
    void EnterProgrammingMode();
    void EnterProgrammingSerialMode();
    void AddMvt(mvt mvtStep);
    void StartExecuting();
};

#endif