#include <Arduino.h>
#include <MVTController.h>
#include <OneButton.h>
#include "../communs.h"

MVTController mvtControl = MVTController();

OneButton FW_Btn = OneButton(FW_Btn_Pin,false,false );
OneButton LT_Btn = OneButton(LT_Btn_Pin,false,false );
OneButton RT_Btn = OneButton(RT_Btn_Pin,false,false );
OneButton DN_Btn = OneButton(DN_Btn_Pin,false,false );
OneButton CR_Btn = OneButton(CR_Btn_Pin,false,false );

void StartProgramming(){
  if(mvtControl.WorkingMode == Idle || mvtControl.WorkingMode == Done) mvtControl.EnterProgrammingMode();
  
}
void StartExecuting(){
  mvtControl.StartExecuting();
}

void FW_Btn_Click(){
  if(mvtControl.WorkingMode == Learning) mvtControl.AddMvt(Forward);
  
}
void DN_Btn_Click(){
  if(mvtControl.WorkingMode == Learning) mvtControl.AddMvt(Backward);
}
void LT_Btn_Click(){
  if(mvtControl.WorkingMode == Learning) mvtControl.AddMvt(Left);
}
void RT_Btn_Click(){
  if(mvtControl.WorkingMode == Learning) mvtControl.AddMvt(Right);
}

void setup() {
  Serial.begin(9600);

  mvtControl.Init();
  CR_Btn.setLongPressIntervalMs(4000);
  CR_Btn.attachDuringLongPress(StartProgramming);
  CR_Btn.attachDoubleClick(StartExecuting);

  FW_Btn.attachClick(FW_Btn_Click);
  LT_Btn.attachClick(LT_Btn_Click);
  RT_Btn.attachClick(RT_Btn_Click);
  DN_Btn.attachClick(DN_Btn_Click);

}

void loop() {
  CR_Btn.tick();
  FW_Btn.tick();
  LT_Btn.tick();
  RT_Btn.tick();
  DN_Btn.tick();

  mvtControl.Execute();
}

