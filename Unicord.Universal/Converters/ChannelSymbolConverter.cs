﻿using DSharpPlus;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Unicord.Universal.Converters
{
    class ChannelSymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if(value is DiscordChannel c)
            {
                if (c.IsNSFW)
                {
                    return "\xE7BA";
                }

                var type = c.Type;
                switch (type)
                {
                    case ChannelType.Text:
                        return "\xE8BD";
                    case ChannelType.Voice:
                        return "\xE767";
                    default:
                        return "";
                }
            }

            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}