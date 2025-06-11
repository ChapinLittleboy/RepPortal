import React, { useState, useEffect } from 'react';
import { ChartComponent, SeriesCollectionDirective, SeriesDirective, Inject, ColumnSeries, Category, Legend, Tooltip } from '@syncfusion/ej2-react-charts';
import customerData from '../../data/topCustomers.json';

export default function TopCustomers() {
  const [customers, setCustomers] = useState([]);
  const [selected, setSelected] = useState(null);

  useEffect(() => {
    setCustomers(customerData.slice(0, 5));
    setSelected(customerData[0]);
  }, []);

  function handleSelect(customer) {
    setSelected(customer);
  }

  return (
    <div className="card">
      <div className="card-header d-flex justify-content-between">
        <h5 className="card-title mb-0">Top Grossing Customers</h5>
      </div>
      <div className="card-body">
        <div className="table-responsive mb-4">
          <table className="table table-bordered table-striped">
            <thead className="table-primary">
              <tr>
                <th>Customer</th>
                <th className="text-end">FY2024 Total</th>
                <th className="text-end">Current YTD</th>
                <th className="text-end">% Change</th>
              </tr>
            </thead>
            <tbody>
              {customers.map(c => (
                <tr key={c.id} onClick={() => handleSelect(c)} className={selected?.id === c.id ? 'table-active' : ''}>
                  <td>{c.name}</td>
                  <td className="text-end">{c.previousYearTotal.toLocaleString()}</td>
                  <td className="text-end">{c.currentYearTotal.toLocaleString()}</td>
                  <td className={`text-end ${c.percentChange >= 0 ? 'text-success' : 'text-danger'}`}>{(c.percentChange * 100).toFixed(1)}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {selected && (
          <ChartComponent height="300px" primaryXAxis={{ valueType: 'Category', majorGridLines: { width: 0 } }} primaryYAxis={{ labelFormat: '{value}K', lineStyle: { width: 0 }, title: 'Sales Amount ($)' }} legendSettings={{ visible: true, position: 'Top' }} tooltip={{ enable: true }}>
            <Inject services={[ColumnSeries, Category, Legend, Tooltip]} />
            <SeriesCollectionDirective>
              <SeriesDirective dataSource={selected.monthlyData} xName='month' yName='previousYearAmount' type='Column' name='FY2024' fill='#6c757d' />
              <SeriesDirective dataSource={selected.monthlyData} xName='month' yName='currentYearAmount' type='Column' name='FY2025' fill='#0d6efd' />
            </SeriesCollectionDirective>
          </ChartComponent>
        )}
      </div>
    </div>
  );
}
