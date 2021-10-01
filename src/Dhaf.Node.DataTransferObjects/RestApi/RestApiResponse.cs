using System.Collections.Generic;

namespace Dhaf.Node
{
    public class RestApiResponse
    {
        public bool Success { get; set; }

        public IEnumerable<RestApiError> Errors { get; set; } = new List<RestApiError>();
    }

    public class RestApiResponse<T> : RestApiResponse
    {
        public T Data { get; set; }
    }
}
