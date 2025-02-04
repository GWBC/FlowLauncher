﻿using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Dictionary
{
    class Iciba
    {
        public class Mean
        {
            public string mean_id { get; set; }
            public string part_id { get; set; }
            public string word_mean { get; set; }
            public string has_mean { get; set; }
            public int split { get; set; }
        }

        public class Part
        {
            public string part_name { get; set; }
            public List<Mean> means { get; set; }
        }

        public class Symbol
        {
            public string symbol_id { get; set; }
            public string word_id { get; set; }
            public string word_symbol { get; set; }
            public string symbol_mp3 { get; set; }
            public List<Part> parts { get; set; }
            public string ph_am_mp3 { get; set; }
            public string ph_en_mp3 { get; set; }
            public string ph_tts_mp3 { get; set; }
            public string ph_other { get; set; }
        }

        public class ServerResponse
        {
            public string word_id { get; set; }
            public string word_name { get; set; }
            public List<Symbol> symbols { get; set; }
        }

        // Chinese to English. Internet access needed.
        public async Task<List<string>> QueryAsync(string word, string token, CancellationToken cancelToken)
        {
            List<string> ret = new List<string>();

            if(string.IsNullOrEmpty(token))
            {
                return ret;
            }

            try
            {
                var dataStream = await Main.Context.API.HttpGetStreamAsync($"http://dict-co.iciba.com/api/dictionary.php?w={word}&key={token}&type=json",
                                                                            cancelToken).ConfigureAwait(false);
                
                var rsp = await JsonSerializer.DeserializeAsync<ServerResponse>(dataStream, cancellationToken: cancelToken).ConfigureAwait(false);

                if (rsp?.symbols == null) return ret;
                foreach (var symbol in rsp.symbols)
                {
                    if (symbol.parts == null) continue;
                    foreach (var part in symbol.parts)
                    {
                        if (part.means == null) continue;
                        foreach (var mean in part.means)
                        {
                            ret.Add(mean.word_mean.ToString());
                        }
                    }
                }
            }
            catch (Exception) { }
            return ret;
        }
    }
}
