using System;
using System.Collections.Generic;
using System.Text;

namespace NetCord.Abar.Bot.Models.Exceptions
{
    public class InvalidInputException(object? input, string message)
        : InvalidRequestException($"{message} Invalid input: '{input ?? "null"}'.")
    {
        public object? Input { get; } = input;
    }
}
