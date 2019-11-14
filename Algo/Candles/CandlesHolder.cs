namespace StockSharp.Algo.Candles
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Ecng.Collections;

	using StockSharp.Messages;

	/// <summary>
	/// Candles holder to create <see cref="Candle"/> instances.
	/// </summary>
	public class CandlesHolder
	{
		private class CandlesSeriesHolder
		{
			private Candle _currentCandle;

			public CandlesSeriesHolder(CandleSeries series)
			{
				Series = series ?? throw new ArgumentNullException(nameof(series));
			}

			public CandleSeries Series { get; }

			public bool UpdateCandle(CandleMessage message, out Candle candle)
			{
				if (message == null)
					throw new ArgumentNullException(nameof(message));

				candle = null;

				if (_currentCandle != null && _currentCandle.OpenTime == message.OpenTime)
				{
					if (_currentCandle.State == CandleStates.Finished)
						return false;

					_currentCandle.Update(message);
				}
				else
					_currentCandle = message.ToCandle(Series);

				candle = _currentCandle;
				return true;
			}
		}

		private readonly CachedSynchronizedDictionary<long, CandlesSeriesHolder> _holders = new CachedSynchronizedDictionary<long, CandlesSeriesHolder>();

		/// <summary>
		/// List of all candles series, subscribed via <see cref="CreateCandleSeries"/>.
		/// </summary>
		public IEnumerable<CandleSeries> AllCandleSeries => _holders.CachedValues.Select(h => h.Series);

		/// <summary>
		/// Clear state.
		/// </summary>
		public void Clear() => _holders.Clear();

		/// <summary>
		/// Create new series tracking.
		/// </summary>
		/// <param name="transactionId">Request identifier.</param>
		/// <param name="series">Candles series.</param>
		public void CreateCandleSeries(long transactionId, CandleSeries series)
		{
			if (transactionId == 0)
				throw new ArgumentNullException(nameof(transactionId));

			_holders.Add(transactionId, new CandlesSeriesHolder(series));
		}

		/// <summary>
		/// Remove series tracking.
		/// </summary>
		/// <param name="transactionId">Request identifier.</param>
		/// <returns>Candles series.</returns>
		public CandleSeries RemoveCandleSeries(long transactionId)
		{
			lock (_holders.SyncRoot)
				return _holders.TryGetAndRemove(transactionId)?.Series;
		}

		/// <summary>
		/// Get request identifier by series.
		/// </summary>
		/// <param name="series">Candles series.</param>
		/// <returns>Request identifier.</returns>
		public long TryGetTransactionId(CandleSeries series)
		{
			if (series == null)
				throw new ArgumentNullException(nameof(series));

			lock (_holders.SyncRoot)
				return _holders.CachedPairs.FirstOrDefault(p => p.Value.Series == series).Key;
		}

		/// <summary>
		/// Get series by request identifier.
		/// </summary>
		/// <param name="transactionId">Request identifier.</param>
		/// <returns>Candles series or <see langword="null"/> if identifier is non exist.</returns>
		public CandleSeries TryGetCandleSeries(long transactionId) => _holders.TryGetValue(transactionId)?.Series;

		/// <summary>
		/// Update candles by new message.
		/// </summary>
		/// <param name="message">Message.</param>
		/// <returns>Candles series.</returns>
		public IEnumerable<Tuple<CandleSeries, Candle, long>> UpdateCandles(CandleMessage message)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			foreach (var subscriptionId in message.GetSubscriptionIds())
			{
				var info = _holders.TryGetValue(subscriptionId);

				if (info == null)
					continue;
					
				if (!info.UpdateCandle(message, out var candle))
					continue;
				
				yield return Tuple.Create(info.Series, candle, subscriptionId);
			}
		}
	}
}