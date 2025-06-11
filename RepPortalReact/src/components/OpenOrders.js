import React from 'react';
import { GridComponent, ColumnsDirective, ColumnDirective, Inject, Page, Sort } from '@syncfusion/ej2-react-grids';
import ordersData from '../../data/openOrders.json';

export default function OpenOrders() {
  return (
    <div className="card">
      <div className="card-header">
        <h5 className="card-title mb-0">Open Orders</h5>
      </div>
      <div className="card-body">
        <GridComponent dataSource={ordersData} allowPaging allowSorting height="400">
          <ColumnsDirective>
            <ColumnDirective field="order" headerText="Order" width="120" textAlign="Right" />
            <ColumnDirective field="customer" headerText="Customer" width="150" />
            <ColumnDirective field="date" headerText="Date" width="100" type="date" format="yMd" textAlign="Right" />
            <ColumnDirective field="amount" headerText="Amount" width="120" format="C2" textAlign="Right" />
          </ColumnsDirective>
          <Inject services={[Page, Sort]} />
        </GridComponent>
      </div>
    </div>
  );
}
