var MSG = {
  title: "BIPES",
  blocks: "Blocs",
  files: "Files",
  shared: "Shared",
  device: "Device",
  linkTooltip: "Sauvegarder et lier aux blocs.",
  runTooltip: "Lancer le programme défini par les blocs dans l’espace de travail.",
  badCode: "Erreur du programme :\n%1",
  timeout: "Nombre maximum d’itérations d’exécution dépassé.",
  trashTooltip: "Jeter tous les blocs.",
  catLogic: "Logique",
  catLoops: "Boucles",
  catMath: "Math",
  catText: "Texte",
  catLists: "Listes",
  catColour: "Couleur",
  catVariables: "Variables",
  catFunctions: "Fonctions",
  listVariable: "liste",
  textVariable: "texte",
  httpRequestError: "Il y a eu un problème avec la demande.",
  linkAlert: "Partagez vos blocs grâce à ce lien:\n\n%1",
  hashError: "Désolé, '%1' ne correspond à aucun programme sauvegardé.",
  xmlError: "Impossible de charger le fichier de sauvegarde.  Peut être a t-il été créé avec une autre version de Blockly?",
  badXml: "Erreur d’analyse du XML :\n%1\n\nSélectionner 'OK' pour abandonner vos modifications ou 'Annuler' pour continuer à modifier le XML.",
  saveTooltip: "Save blocks to file.",
  loadTooltip: "Load blocks from file.",
  notificationTooltip: "Notifications panel.",
  ErrorGET: "Unable to load requested file.",
  invalidDevice: "Invalid device.",
  languageTooltip: "Change language.",
  noToolbox: "The device has no toolbox set.",
  networkTooltip: "Connect through network (WebREPL, http).",
  serialTooltip: "Connect through serial/USB or Bluetooth (Web Serial API, https).",
  toolbarTooltip: "Show toolbar",
  wrongDevicePin: "Check pins, target device changed!",
  notDefined: "not defined",
  editAsFileValue: "Edit as a file",
  editAsFileTooltip: "Edit python code and save to device's filesystem.",
  forumTooltip: "Help forum.",
  accountTooltip: "Your projects and settings.",
  blocksLoadedFromFile: "Blocks loaded from file '%1'.",
  deviceUnavailable: "Device '%1' unavailable.",
  notConnected: "No connection to send data.",
  serialFroozen: "Serial connection is unresponsive.",
  notAvailableFlag: "$1 is not available on your browser.\r\nPlease make sure the $1 flag is enabled.",

//Blocks
  block_delay: "délai",
  seconds: "secondes",
  milliseconds: "millisecondes",
  microseconds: "microsecondes",
  block_delay_ms:"délai millisecondes",
  block_delay_micros:"délai microsecondes",
  block_delay_Get_milis_counter: "Obtenir le compteur de millisecondes",
  block_delay_Compute_time_difference: "Calculer la différence de temps",

  start: "Debut",
  end:"Fin",
  do:"faire",
  every : "chaque",
  oncein:"une fois dans",
  stopTimer:"Arrêter le Timer",
  to: "à",
  setpin: "set output pin",
  pin: "pin",
  read_digital_pin: "read digital input",
  read_analog_pin: "read analog input",
  show_iot: "show on IoT tab",
  data: "value",
  set_rtc: "set date and time",
  get_rtc: "get date and time",
  year: "year",
  month: "month",
  day: "day",
  hour: "hour",
  minute: "minute",
  second: "second",
  wifi_scan: "scan wifi networks",
  wifi_connect: "connect to wifi network",
  wifi_name: "network name",
  wifi_key: "key/password",
  easymqtt_start: "EasyMQTT Start",
  easymqtt_publish: "EasyMQTT Publish Data",
  topic: "topic",
  session_id: "session ID",
  file_open: "open File",
  file_name: "file name",
  file_mode: "mode",
  file_binary: "open in binary mode",
  file_close: "close file",
  file_write_line: "write line to file",
  file_line: "line",
  try1: "try",
  exp1: "except",
  ntp_sync: "sync date and time with NTP",
  timezone: "timezone",
  project_info: "Project Info",
  project_info_author: "Author",
  project_info_desc: "Description",
  easymqtt_subscribe: "EasyMQTT subscribe to topic",
  when: "when",
  data_received: "is received",
  easymqtt_receive: "EasyMQTT receive data",
  relay: "relay",
  on: "turn on",
  off: "turn off",
  relay_on: "relay on pin",
  yes: "yes",
  no: "no",
  wait_for_data: "wait for data",
  dht_start: "Start DHT Sensor",
  dht_measure: "update DHT11/22 sensor reading",
  dht_temp: "get DHT11/22 temperature",
  dht_humi: "get DHT11/22 humidity",
  type: "type",



  checkbutton:"Vérifier le bouton:",

  up:"Haut",
  down:"Bas",
  left:"Gauche",
  right:"Droite",
  center:"Centre",

  checkobstaclsensor:"Vérifier le capteur d'obstacle",
  checkgroundsensor:"Vérifier si le capteur détecte le sol",

  readTempr:"Lire la température",
  unit:"Unité",


  onBoot:"quand CarthaBot démarre",
  onBootTip:"Accroche tes blocs en dessous. Ils s'exécutent quand le CarthaBot s'allume.",

  initIrRec:"Initialiser le récepteur IR",
  readfromir:"Lire les données du récepteur IR",
  timeout:"Délai d'expiration (ms)",
  infinetduration:"(0 pour une durée infinie)",

  initRGB:"Init RGB",
  color:"Couleur",
  ledNumber:"Numéro de LED",

  write:"Ecrire",

  red:"Rouge",
  green:"Vert",  
  blue:"Bleu",


  initMotorLeft:"Initier le moteur gauche",
  initMotorRight:"Initier le moteur droit",

  startMotorLeft:"Démarrer le moteur gauche",
  startMotorRight:"Démarrer le moteur droit",

  stopMotorLeft:"Arrêter le moteur gauche",
  stopMotorRight:"Arrêter le moteur droit",

  reverseLeft:"Inverser le moteur gauche",
  reverseRight:"Inverser le moteur droit",

  setmotorLeft:"Régler la vitesse du moteur gauche",
  setmotorRight:"Régler la vitesse du moteur droit",

  forward:"Avancer",
  backward:"Reculer",

  speed:"Vitesse",

  readmicValue:"Lire la valeur du microphone",
  detectsound:"Détecter le son",
  threshold:"Seuil",


  tone:"Ton (Hz)",
  frequency: "Fréquence",
  duration: "Durée (s)",
  Pplaymusic: "Jouer une note de musique",
  note: "Note",
  playsong: "Jouer une chanson",
  song: "Chanson",
  music: "Chanson",
  MusicOptions: "Liste d'options musicales",

  initAccel:"Initier l'accéléromètre",  
  acceX:"Accélération - X axis",
  acceY:"Accélération - Y axis",
  acceZ:"Accélération - Z axis",
  acceXg:"Accélération en g - X axis",
  acceYg:"Accélération en g - Y axis",
  acceZg:"Accélération en g - Z axis",


  usethesdcard:"Utiliser la carte SD",
  mountpath:"Chemin de montage",
  filename:"Nom du fichier",
  countlineinfile:"Compter les lignes dans le fichier",
  readfilecontent:"Lire le contenu du fichier",
  listfiles:"Lister les fichiers sur la carte SD",
  readline:"Lire la ligne",
  creatfile:"Créer un fichier",
  deletfile:"Supprimer le fichier",
  clearfile:"Effacer le fichier",
  writefile:"Écrire dans le fichier",
  content:"Contenu",
  write:"write",
  append:"ajouter",








//Network
  net_http_get: "HTTP GET Request",
  net_http_get_status: "HTTP Status code",
  net_http_get_content: "HTTP Response content",

//Splash screen
  splash_welcome: "Bienvenue au BIPES!",
  splash_footer: "Do not show this screen again",
  splash_close: "Close",
  splash_message: "<p><b>BIPES: Block based Integrated Platform for Embedded Systems</B> allows text and block based programming for several types of embedded systems and Internet of Things modules using MicroPython, CircuitPython, Python or Snek. You can connect, program, debug and monitor several types of boards using network, USB or Bluetooth. Please check a list of <a href=https://bipes.net.br/wp/boards/>compatible boards here</a>. Compatible boards include STM32, ESP32, ESP8266, Raspberry Pi Pico and even Arduino.  <p><b>BIPES</b> is fully <a href=https://bipes.net.br/wp/development/>open source</a> and based on HTML and JavaScript, so no software install or configuration is needed and you can use it offline! We hope BIPES is useful for you and that you can enjoy using BIPES. If you need help, we now have a <a href=https://github.com/BIPES/BIPES/discussions>discussion forum</a>, where we also post <a href=https://github.com/BIPES/BIPES/discussions/categories/announcements>new features and announcements about BIPES</a>. Feel free to use it! Also, we invite you to use the forum to leave feedbacks and suggestions for BIPES!</p><p>Now you can easily load MicroPython on your ESP32 or ESP8226 to use with BIPES: <a href=https://bipes.net.br/flash/esp-web-tools/>https://bipes.net.br/flash/esp-web-tools/</a></p> <p>Thank you from the BIPES Team!</p>"

  

  

};

//Toolbox categories
Blockly.Msg['CAT_START'] = "Départ";
Blockly.Msg['CAT_TIMING'] = "Chronométrage";
Blockly.Msg['CAT_MACHINE'] = "Machine";
Blockly.Msg['CAT_DISPLAYS'] = "Affichages";
Blockly.Msg['CAT_SENSORS'] = "Capteurs";
Blockly.Msg['CAT_OUTPUTS'] = "Sorties / Actionneurs";
Blockly.Msg['CAT_COMM'] = "Communication";
Blockly.Msg['CAT_TEMP_HUMI'] = "Température et Humidité";
Blockly.Msg['CAT_PRESS'] = "Pression";
Blockly.Msg['CAT_FILES'] = "Fichiers";
Blockly.Msg['CAT_NET'] = "Réseau et Internet";
Blockly.Msg['CAT_CONTROL'] = "Contrôle";
Blockly.Msg['CAT_IMU'] = "Mesure Inertielle";
Blockly.Msg['CAT_AIR'] = "Qualité de l'air";
Blockly.Msg['CAT_ULTRASOUND'] = "Ultrasons";

Blockly.Msg['CAT_BUTTONS'] = "Boutons";
Blockly.Msg['CAT_TEMPERATURE'] = "Température";
Blockly.Msg['CAT_IR_RECEIVER'] = "Récepteur IR";
Blockly.Msg['CAT_RGB_LED'] = "LED RGB";
Blockly.Msg['CAT_MOTORS'] = "Moteurs";
Blockly.Msg['CAT_MICROPHONE'] = "Microphone";
Blockly.Msg['CAT_SOUNDS'] = "Sons";
Blockly.Msg['CAT_ACCELEROMETER'] = "Accéléromètre";
Blockly.Msg['CAT_SD_CARD'] = "Carte SD";