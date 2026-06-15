// MVTController.cpp
#include "MVTController.h"
#include <RGBController.h>
#include "../communs.h"
#include <MotorControl.h>
#include <CRC16.h>

CRC16 crc;
void MVTController::Init(){
    curentExcecStep = 0;
    curentSaveStep = 0;
    WorkingMode = Idle;
    rgb.Init();
    rgb.SetStaticColor(rgb.strip.Color(0,0,0));
    rgb.strip.setBrightness(100);  
    motors.Init();
}
MVTController::MVTController() : rgb(),sound(),motors(){

}
unsigned long previousMillis = 0; // Store the last time the timer was updated
const unsigned long interval = 1000; // 10 seconds (10000 milliseconds)

void MVTController::Execute(){
    if(WorkingMode == Runing){
        unsigned long currentMillis = millis();
        if (currentMillis - previousMillis >= interval) {
            previousMillis = currentMillis; 

        }
        if (curentExcecStep < curentSaveStep)
        {
            Serial.println(mvtTable[curentExcecStep]);
            switch (mvtTable[curentExcecStep]) {
            case 1:
                motors.GoForward();
                break;
            case 2:
                motors.GoBack();
                break;
            case 3:
                motors.TurnFullLeft();
                break;
            case 4:
                motors.TurnFullRight();
                break;
            default:
                motors.Stop();
                break;
            }
            curentExcecStep++;
        }
        else
        {
            WorkingMode = Done;
            rgb.SetStaticColor(rgb.strip.Color(255, 0, 0));
            motors.Stop();
        }
    }

    if (Serial.available() > 0 || WorkingMode != Runing) {

        // Read the header
        if (Serial.read() == 0xAA) {
          ClearTable();
          EnterProgrammingSerialMode();
          // Read the length
          int length = Serial.read();
          byte payload[length];
          curentSaveStep=0;
          // Read the payload
          for (int i = 0; i < length; i++) {
            payload[i] = Serial.read();
          }
          
          // Read the CRC
          byte crcHigh = Serial.read();
          byte crcLow = Serial.read();
          unsigned short receivedCrc = (crcHigh << 8) | crcLow;
          
          // Compute the CRC
          //unsigned short computedCrc = crc.XModemCrc(payload, length);
          unsigned short computedCrc  = receivedCrc;
          // Validate the CRC
          if (receivedCrc == computedCrc) {
            Serial.println("Data received correctly!");
            // Process the payload
            for (int i = 0; i < length; i++) {
              rgb.SetStaticColor(rgb.strip.Color(255,255,255)); 

              switch (payload[i]) {
                case 1:
                    AddMvt(Forward);
                  break;
                case 2:
                    AddMvt(Backward);
                    break;
                case 3:
                    AddMvt(Left);
                    break;
                case 4:
                    AddMvt(Right);
                    break;
              }
            }
            sound.PlayChangeMode();
          } else {
            Serial.println("CRC check failed!");
          }
        }
      }
}

void MVTController::ClearTable(){
    curentExcecStep = 0;
    curentSaveStep = 0;
    WorkingMode = Idle;
    for(int i=0;i<MaxMouvment;i++){
        mvtTable[i] = Empty;
    }
}


void MVTController::EnterProgrammingMode()
{
    if(WorkingMode == Learning)return;
    ClearTable();
    rgb.SetStaticColor(rgb.strip.Color(255,255,255));  
    WorkingMode = Learning;
    sound.PlayCanChangeMode();
}

void MVTController::EnterProgrammingSerialMode()
{
    if(WorkingMode == LearningSerial)return;
    ClearTable();
    rgb.SetStaticColor(rgb.strip.Color(255,255,255));  
    WorkingMode = LearningSerial;
    sound.PlayCanChangeMode();
}

void  MVTController::AddMvt(mvt mvtStep){
    if(WorkingMode == Learning && curentSaveStep < MaxMouvment){
        mvtTable[curentSaveStep] = mvtStep;
        curentSaveStep++;
        sound.PlayChangeMode();
    }
    else if(WorkingMode == LearningSerial && curentSaveStep < MaxMouvment)
    {
        mvtTable[curentSaveStep] = mvtStep;
        curentSaveStep++;
    }
}

void MVTController::StartExecuting()
{
    if(WorkingMode == Learning || WorkingMode == LearningSerial  || WorkingMode == Done){
        Serial.println("Start");
        curentExcecStep =0;
        sound.PlayCantChangeMode();
        rgb.SetStaticColor(rgb.strip.Color(0,0,255));  
        WorkingMode = Runing;

    }
}