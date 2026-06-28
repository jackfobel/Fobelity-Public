using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Seam;
using HvacModeSettingEnum = Seam.Model.DevicePropertiesCurrentClimateSetting.HvacModeSettingEnum;

namespace Fobelity.Home.Automation.Adapters.Seam
{
  public static class SeamHvacModeExtensions
  {
    public static string ToApiString(this HvacModeSettingEnum e) => e switch
    {
      HvacModeSettingEnum.Off => "off",
      HvacModeSettingEnum.Heat => "heat",
      HvacModeSettingEnum.Cool => "cool",
      HvacModeSettingEnum.HeatCool => "heat_cool",
      HvacModeSettingEnum.Eco => "eco",
      _ => "unknown"
    };
  }
}
