using System;
using System.Collections.Generic;
using System.Text;

namespace NetCord.Abar.Bot.Models.Exceptions
{
    public class InvalidRequestException(string message) : Exception(message);
}
