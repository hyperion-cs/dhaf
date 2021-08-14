using System.Collections.Generic;
using System.Linq;

namespace Dhaf.CloudflareSwitch.DataTransferObjects
{
    public abstract class ResultBaseDto<T> where T : class
    {
        public bool Success { get; set; }
        public List<ErrorDto> Errors { get; set; }

        public string PrettyErrors(string action)
        {
            var separator = "\n";
            var intro = $"Errors occurred ({Errors.Count} pcs.) in the action \"{action}\":";
            var errors = string.Join(separator, Errors.Select(x => $"Error {x.Code}: {x.Message}"));

            return $"{intro}\n{errors}";
        }
    }

    public class ResultDto<T> : ResultBaseDto<T> where T : class
    {
        public T Result { get; set; }
    }

    public class ResultCollectionDto<T> : ResultBaseDto<T> where T : class
    {
        public List<T> Result { get; set; }
    }
}
