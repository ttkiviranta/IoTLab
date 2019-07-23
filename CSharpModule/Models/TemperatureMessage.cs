using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpModule.Models
{
  //  public class TemperatureMessage
  //  {
        public class MessageBody
        {
            public Machine machine { get; set; }
            public Ambient ambient { get; set; }
            public string timeCreated { get; set; }
        }
        public class Machine
        {
            public double temperature { get; set; }
            public double pressure { get; set; }
        }
        public class Ambient
        {
            public double temperature { get; set; }
            public int humidity { get; set; }
        }
    //}
}
