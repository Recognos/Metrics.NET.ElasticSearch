using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Metrics.ElasticSearch
{

    [DataContract]
    public class ElasticSearchNodeInfo
    {

        [DataMember]
        Version version = null;

        public int MajorVersionNumber 
        { 
            get 
            {
                var major = version.Number.Substring(0, version.Number.IndexOf('.'));
                return int.Parse(major);
            } 
        }
    }

    [DataContract]
    class Version
    {
        [DataMember]
        string number = "";

        public string Number { get { return number; } }

    }
}
