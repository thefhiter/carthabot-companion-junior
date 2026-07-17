var lis2hh12Blob = new Blob([
"from machine import I2C, Pin\n"+
"import time\n"+
"\n"+
"class LIS2DH12:\n"+
"    # LIS2DH12 I2C address\n"+
"    DEFAULT_ADDR = 0x19\n"+
"\n"+
"    # LIS2DH12 register addresses\n"+
"    REG_CTRL1 = 0x20\n"+
"    REG_OUT_X_L = 0x28\n"+
"    REG_OUT_X_H = 0x29\n"+
"    REG_OUT_Y_L = 0x2A\n"+
"    REG_OUT_Y_H = 0x2B\n"+
"    REG_OUT_Z_L = 0x2C\n"+
"    REG_OUT_Z_H = 0x2D\n"+
"\n"+
"    def __init__(self, i2c, address=DEFAULT_ADDR):\n"+
"        self.i2c = i2c\n"+
"        self.address = address\n"+
"        self.initialize_sensor()\n"+
"\n"+
"    def initialize_sensor(self):\n"+
"        self.write_register(self.REG_CTRL1, 0x57)  # 100Hz, normal mode, all axes enabled\n"+
"\n"+
"    def write_register(self, reg, data):\n"+
"        self.i2c.writeto_mem(self.address, reg, bytes([data]))\n"+
"\n"+
"    def read_register(self, reg):\n"+
"        return int.from_bytes(self.i2c.readfrom_mem(self.address, reg, 1), 'little')\n"+
"\n"+
"    def read_accel_data(self):\n"+
"        x_l = self.read_register(self.REG_OUT_X_L)\n"+
"        x_h = self.read_register(self.REG_OUT_X_H)\n"+
"        y_l = self.read_register(self.REG_OUT_Y_L)\n"+
"        y_h = self.read_register(self.REG_OUT_Y_H)\n"+
"        z_l = self.read_register(self.REG_OUT_Z_L)\n"+
"        z_h = self.read_register(self.REG_OUT_Z_H)\n"+
"\n"+        
"        x = (x_h << 8) | x_l\n"+
"        y = (y_h << 8) | y_l\n"+
"        z = (z_h << 8) | z_l\n"+
"\n"+        
"        # Convert to signed 16-bit values\n"+
"        if x > 32767:\n"+
"            x -= 65536\n"+
"        if y > 32767:\n"+
"            y -= 65536\n"+
"        if z > 32767:\n"+
"            z -= 65536\n"+
"\n"+        
"        return x, y, z\n"+
"\n"+
"    def read_accel_data_g(self):\n"+
"        x, y, z = self.read_accel_data()\n"+
"        x_g = self.convert_to_g(x)\n"+
"        y_g = self.convert_to_g(y)\n"+
"        z_g = self.convert_to_g(z)\n"+
"        return x_g, y_g, z_g\n"+
"\n"+
"    def convert_to_g(self, raw_value):\n"+
"        # Sensitivity at ±2g is 1 mg/LSB\n"+
"        return raw_value / 1000.0\n"

], {type: 'text'});